using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Routing;
using Akka.Bootstrap.Docker;
using Akka.Configuration;
using Akka.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Akka.Cluster;
using Akka.Cluster.Tools.Singleton;
using Akka.ClusterPingPong.Actors;

namespace Akka.ClusterPingPong
{
    /// <summary>
    /// <see cref="IHostedService"/> that runs and manages <see cref="ActorSystem"/> in background of application.
    /// </summary>
    public class AkkaService : IHostedService
    {
        private ActorSystem _clusterSystem;
        private readonly IServiceProvider _serviceProvider;

        // needed to help guarantee clean shutdowns
        private readonly IHostApplicationLifetime _lifetime;

        public IActorRef BenchmarkCoordinatorManager {get; private set;}

        public IActorRef BenchmarkCoordinator {get; private set;}

        public IActorRef BenchmarkHost {get; private set;}

        public IActorRef BenchmarkHostRouter {get; private set;}

        public AkkaService(IServiceProvider serviceProvider, IHostApplicationLifetime lifetime)
        {
            _serviceProvider = serviceProvider;
            _lifetime = lifetime;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
             var config = ConfigurationFactory.ParseString(File.ReadAllText("app.conf")).BootstrapFromDocker();
             var bootstrap = BootstrapSetup.Create()
                .WithConfig(config) // load HOCON
                .WithActorRefProvider(ProviderSelection.Cluster.Instance); // launch Akka.Cluster

            // N.B. `WithActorRefProvider` isn't actually needed here - the HOCON file already specifies Akka.Cluster

            // enable DI support inside this ActorSystem, if needed
            var diSetup = ServiceProviderSetup.Create(_serviceProvider);

            // merge this setup (and any others) together into ActorSystemSetup
            var actorSystemSetup = bootstrap.And(diSetup);

            ThreadPool.GetMinThreads(out var workerThreads, out var completionThreads);
            Console.WriteLine("Min threads: {0}, Min I/O threads: {1}", workerThreads, completionThreads);
            ThreadPool.SetMinThreads(0, 0);

            // start ActorSystem
            _clusterSystem = ActorSystem.Create("ClusterSys", actorSystemSetup);

            // instantiate actors
            BenchmarkHostRouter = _clusterSystem.ActorOf(Props.Empty.WithRouter(FromConfig.Instance), "host-router");

            BenchmarkCoordinatorManager = _clusterSystem.ActorOf(ClusterSingletonManager.Props(
                singletonProps: Props.Create(() => new BenchmarkCoordinator(2, 6, BenchmarkHostRouter)),
                terminationMessage: PoisonPill.Instance,
                settings: ClusterSingletonManagerSettings.Create(_clusterSystem)), "coordinator");

            BenchmarkCoordinator = _clusterSystem.ActorOf(ClusterSingletonProxy.Props(
                singletonManagerPath: "/user/coordinator",
                settings: ClusterSingletonProxySettings.Create(_clusterSystem)), "coordinator-proxy");

            BenchmarkHost = _clusterSystem.ActorOf(Props.Create(() => new BenchmarkHost(BenchmarkCoordinator)), "host");           

            Akka.Cluster.Cluster.Get(_clusterSystem).RegisterOnMemberRemoved(() => {
                _lifetime.StopApplication(); // when the ActorSystem terminates, terminate the process
            });
            
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            // strictly speaking this may not be necessary - terminating the ActorSystem would also work
            // but this call guarantees that the shutdown of the cluster is graceful regardless
             await CoordinatedShutdown.Get(_clusterSystem).Run(CoordinatedShutdown.ClrExitReason.Instance);
        }
    }
   
}
