using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Bootstrap.Docker;
using Akka.Configuration;
using Akka.Persistence.Sqlite;
using Akka.PersistentCart.Actors;

namespace Akka.PersistentCart
{
    class Program
    {
        internal static ActorSystem ActorSystem;
        internal static IActorRef ReportingActor;
        internal static IActorRef CounterActor;

        internal static Stack<States.IState> StateStack = new Stack<States.IState>();

        static async Task Main(string[] args)
        {
            var config = ConfigurationFactory
                .ParseString(File.ReadAllText("app.conf"))
                .WithFallback(SqlitePersistence.DefaultConfiguration())
                .BootstrapFromDocker();

            var bootstrap = BootstrapSetup.Create()
                .WithConfig(config); // load HOCON

            ThreadPool.GetMinThreads(out var workerThreads, out var completionThreads);
            Console.WriteLine("Min threads: {0}, Min I/O threads: {1}", workerThreads, completionThreads);
            ThreadPool.SetMinThreads(0, 0);

            // start ActorSystem
            ActorSystem = ActorSystem.Create("PersistentSys", bootstrap);

            // instantiate actors
            ReportingActor = ActorSystem.ActorOf<ReportingActor>();
            CounterActor = ActorSystem.ActorOf<PersistentCounterActor>();

            await MainLoop();

            await CoordinatedShutdown.Get(ActorSystem).Run(CoordinatedShutdown.ClrExitReason.Instance);
        }

        private static async Task MainLoop()
        {
            StateStack.Push(new States.MainMenu());
            StateStack.Peek().PrintMenu();
            while (true)
            {
                if (!Console.KeyAvailable)
                {
                    await Task.Delay(100);
                    continue;
                }

                var key = Console.ReadKey(true).Key;
                if (StateStack.Peek().Handlers.TryGetValue(key, out var handler))
                {
                    await handler();
                }
                else
                {
                    Console.WriteLine($"Unknown menu item [{key}]");
                }

                if (StateStack.Count == 0)
                    break;

                StateStack.Peek().PrintMenu();
            }

        }
    }
}
