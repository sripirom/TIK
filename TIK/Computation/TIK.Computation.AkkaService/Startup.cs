﻿using System;
using System.IO;
using Akka.Actor;
using Microsoft.AspNetCore.Builder;
using Serilog;
using TIK.Applications.Online.BackLogs;
using TIK.Applications.Online.CommonStocks;
using TIK.Applications.Online.EodStocks;
using TIK.Applications.Online.Jobs;
using TIK.Applications.Online.Members;
using TIK.Core.Logging;
using TIK.Domain.Membership;
using TIK.Domain.TheSet;
using TIK.Integration.Batch;
using TIK.Integration.WebApi.Batch;
using TIK.Persistance.ElasticSearch.Mocks;

namespace TIK.Computation.AkkaService
{
    public class Startup
    {
        private static ILog _logger = LogProvider.For<Program>();


        private static AkkaStateService ActorSystemInstance;

        public void Configure(IApplicationBuilder app)
        {
            Log.Logger = new LoggerConfiguration()
                      .MinimumLevel.Verbose()
                      .WriteTo.LiterateConsole()
                      .WriteTo.RollingFile("logs\\log-{Date}.txt")
                      .CreateLogger();

            ActorSystemInstance = new AkkaStateService();



            ActorSystemInstance.Start();

        
        }
    }

    public class AkkaStateService
    {
        private ActorSystem ActorSystemInstance;

        public void Start()
        {
            try
            {
                string host = @"127.0.0.1:5301";

                var huconConfig = Path.Combine(Directory.GetCurrentDirectory(), "Hucon.txt");
                var config = HoconLoader.FromFile(huconConfig);
                ActorSystemInstance = ActorSystem.Create("OnlineSystem", config);
                IMemberRepository memberRepository = new MockMemberRepository();
                ICommonStockRepository commonStockRepository = new MockCommonStockRepository();
                ICommonStockInfoRepository commonStockInfoRepository = new MockCommonStockInfoRepository();
                IEodRepository eodRepository = new MockEodRepository();

                IBatchPublisher batchPublisher = new BatchPublisher(new Uri("http://localhost:5102/"));

                var memberController = MemberActorProvider.CreateInstance(ActorSystemInstance, memberRepository);
                var jobsActorProvider = JobsActorProvider.CreateInstance(ActorSystemInstance, batchPublisher);
                var backLogsActor = BackLogsActorProvider.CreateInstance(ActorSystemInstance, new JobsActorProvider(ActorSystemInstance, host));
                var commonStocksActor = CommonStocksProvider.CreateInstance(ActorSystemInstance, commonStockRepository, commonStockInfoRepository);
                var eodStocksActor = EodStocksProvider.CreateInstance(ActorSystemInstance, eodRepository);
                var commonStockRouteActor = CommonStockRouteProvider.CreateInstance(ActorSystemInstance, commonStocksActor, eodStocksActor);
            } 
            catch (Exception ex)
            {
                throw ex;
            }

        }

        public void Stop()
        {
            ActorSystemInstance.Terminate().Wait(TimeSpan.FromSeconds(2));
        }
    }
}
