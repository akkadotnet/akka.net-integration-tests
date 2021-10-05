using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Bootstrap.Docker;
using Akka.Configuration;
using Akka.DependencyInjection;
using Akka.Management;
using Akka.Management.Cluster.Bootstrap;
using Microsoft.Extensions.Hosting;
using Petabridge.Cmd.Cluster;
using Petabridge.Cmd.Host;
using Petabridge.Cmd.Remote;

namespace Akka.Cluster.Bootstrap
{
    public class AkkaService: IHostedService
    {
        private ActorSystem _system;
        public Task TerminationHandle => _system.WhenTerminated;
        private readonly IServiceProvider _serviceProvider;

        // needed to help guarantee clean shutdowns
        private readonly IHostApplicationLifetime _lifetime;

        public IActorRef ClusterListener {get; private set;}

        public AkkaService(IServiceProvider serviceProvider, IHostApplicationLifetime lifetime)
        {
            _serviceProvider = serviceProvider;
            _lifetime = lifetime;
        }
        
        public Task StartAsync(CancellationToken cancellationToken)
        {
            Config config;
            switch (Environment.GetEnvironmentVariable("DEPLOY_TYPE"))
            {
                case "config":
                    var hostName = Environment.GetEnvironmentVariable("CLUSTER_IP");
                    config = ConfigurationFactory.ParseString($"akka.management.http.hostname = {hostName}").
                        WithFallback(ConfigurationFactory.ParseString(File.ReadAllText("app.conf")));
                    break;
                case "dynamic":
                    config = ConfigurationFactory.ParseString(File.ReadAllText("app-dynamic.conf"));
                    break;
                default:
                    throw new Exception(
                        "Unknown 'DEPLOY_TYPE' environment variable value. Valid values are 'config' and 'dynamic'");
            }

            config = config
                .WithFallback(ClusterBootstrap.DefaultConfiguration())
                .WithFallback(AkkaManagementProvider.DefaultConfiguration())
                .BootstrapFromDocker();
            var bootstrap = BootstrapSetup.Create()
                .WithConfig(config) // load HOCON
                .WithActorRefProvider(ProviderSelection.Cluster.Instance); // launch Akka.Cluster

            // enable DI support inside this ActorSystem, if needed
            var diSetup = DependencyResolverSetup.Create(_serviceProvider);

            // merge this setup (and any others) together into ActorSystemSetup
            var actorSystemSetup = bootstrap.And(diSetup);
            var systemName = Environment.GetEnvironmentVariable("ACTORSYSTEM")?.Trim();

            _system = ActorSystem.Create(systemName, actorSystemSetup);

            // start https://cmd.petabridge.com/ for diagnostics and profit
            var pbm = PetabridgeCmd.Get(_system); // start Pbm
            pbm.RegisterCommandPalette(ClusterCommands.Instance);
            pbm.RegisterCommandPalette(new RemoteCommands());
            pbm.Start(); // begin listening for PBM management commands
            
            ClusterListener = _system.ActorOf(Akka.Cluster.Bootstrap.ClusterListener.Props(), "listener");
            
            Cluster.Get(_system).RegisterOnMemberRemoved(() => {
                _lifetime.StopApplication(); // when the ActorSystem terminates, terminate the process
            });
            
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await CoordinatedShutdown.Get(_system).Run(CoordinatedShutdown.ClrExitReason.Instance);
        }
    }
}