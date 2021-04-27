using System.Threading.Tasks;
using Akka.ClusterPingPong;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Akka.Integration
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddLogging();
                    services.AddHostedService<AkkaService>(); // runs Akka.NET
 
                })
                .ConfigureLogging((hostContext, configLogging) =>
                {
                    configLogging.AddConsole();
 
                })
                .UseConsoleLifetime()
                .Build();

            await host.RunAsync();
        }
    }
   
}
