using Microsoft.Extensions.DependencyInjection;

namespace TIK.Applications.Online.CommonStocks
{
    public static class Services
    {
        public static void AddCommonStockServices(this IServiceCollection services)
        {
            //services.AddSingleton<JobsActorProvider>();
            services.AddSingleton<Routes.GetCommonStocks>();
            services.AddSingleton<Routes.GetStockInfo>();
            services.AddSingleton<Routes.AddDiscussion>();
            services.AddSingleton<Routes.GetDiscussion>();
        }
    }
}
