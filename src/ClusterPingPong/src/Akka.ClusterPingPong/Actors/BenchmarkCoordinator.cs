using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;

namespace Akka.ClusterPingPong.Actors
{
    public class BenchmarkCoordinator : UntypedActor
    {
        protected override void OnReceive(object message){

        }
    }

    // One of these runs on every node, to deploy the relevant actors
    public sealed class BenchmarkRunner : UntypedActor{
        private HashSet<IActorRef> _benchmarkActors = new HashSet<IActorRef>();
        private HashSet<IActorRef> _echoActors = new HashSet<IActorRef>();
        private readonly ILoggingAdapter _log = Context.GetLogger();

        public Akka.Cluster.Cluster Cluster { get; } = Akka.Cluster.Cluster.Get(Context.System);

        protected override void OnReceive(object message){

        }

        protected void BenchmarkRunning(object message){

        }

        protected override void PreStart(){
            Cluster.Subscribe(Context.Self, );
        }
    }
}
