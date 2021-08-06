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
            var config = ConfigurationFactory.ParseString(File.ReadAllText("app.conf"))
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

            var pbm = PetabridgeCmd.Get(_system);
            pbm.RegisterCommandPalette(ClusterCommands.Instance);
            pbm.RegisterCommandPalette(RemoteCommands.Instance);
            pbm.Start();
            
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