﻿using System;
using System.Collections.Generic;
using System.Linq;
using Consul;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;


namespace TIK.Core.Governance.ServiceDiscovery
{
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseConsulRegisterService(this IApplicationBuilder app)
        {
            var appLife = app.ApplicationServices.GetRequiredService<IApplicationLifetime>() ?? throw new ArgumentException("Missing dependency", nameof(IApplicationLifetime));
            var options = app.ApplicationServices.GetRequiredService<IOptions<ServiceDiscoveryOptions>>() ?? throw new ArgumentException("Missing dependency", nameof(IOptions<ServiceDiscoveryOptions>));
            var serviceOptions = ServiceDiscoveryEnvFactory.Get(options.Value);

            var consul = app.ApplicationServices.GetRequiredService<IConsulClient>() ?? throw new ArgumentException("Missing dependency", nameof(IConsulClient));
            var loggerFactory = app.ApplicationServices.GetRequiredService<ILoggerFactory>();

            var logger = loggerFactory.CreateLogger("ServiceDiscoveryBuilder");

            if (string.IsNullOrEmpty(serviceOptions.ServiceName))
            {
                throw new ArgumentException("Service Name must be configured", nameof(serviceOptions.ServiceName));
            }

            IEnumerable<Uri> addresses = null;
            if (serviceOptions.Endpoints != null && serviceOptions.Endpoints.Length > 0)
            {
                logger.LogInformation($"Using {serviceOptions.Endpoints.Length} configured endpoints for service registration.");
                addresses = serviceOptions.Endpoints.Select(p => new Uri(p));
            }
            else
            {
                logger.LogInformation($"Trying to use server.Features to figure out the service endpoints for service registration.");
                var features = app.Properties["server.Features"] as FeatureCollection;
                addresses = features.Get<IServerAddressesFeature>()
                    .Addresses
                    .Select(p => new Uri(p)).ToArray();
            }

            logger.LogInformation($"Found {addresses.Count()} endpoints: {string.Join(",", addresses.Select(p => p.OriginalString))}.");

            foreach (var address in addresses)
            {
                var serviceId = $"{serviceOptions.ServiceName}_{address.Host}:{address.Port}";

                logger.LogInformation($"Registering service {serviceId} for address {address}.");

                var serviceChecks = new List<AgentServiceCheck>();

                if (!string.IsNullOrEmpty(serviceOptions.HealthCheckTemplate))
                {
                    var healthCheckUri = new Uri(address, serviceOptions.HealthCheckTemplate).OriginalString;
                    serviceChecks.Add(new AgentServiceCheck()
                    {
                        Status = HealthStatus.Passing,
                        DeregisterCriticalServiceAfter = TimeSpan.FromMinutes(1),
                        Interval = TimeSpan.FromSeconds(5),
                        HTTP = healthCheckUri
                    });

                    logger.LogInformation($"Adding healthcheck for service {serviceId}, checking {healthCheckUri}.");
                }

                var registration = new AgentServiceRegistration()
                {
                    Checks = serviceChecks.ToArray(),
                    Address = address.Host,
                    ID = serviceId,
                    Name = serviceOptions.ServiceName,
                    Port = address.Port
                };

                consul.Agent.ServiceRegister(registration).GetAwaiter().GetResult();

                appLife.ApplicationStopping.Register(() =>
                {
                    consul.Agent.ServiceDeregister(serviceId).GetAwaiter().GetResult();
                });
            }

            return app;
        }
    }
}