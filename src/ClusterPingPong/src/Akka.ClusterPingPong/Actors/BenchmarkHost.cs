using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using static Akka.ClusterPingPong.Messages.BenchmarkProtocol;

namespace Akka.ClusterPingPong.Actors
{
    // We create one of these per node pair per benchmark round
    public class BenchmarkHost : ReceiveActor
    {
        public Akka.Cluster.Cluster Cluster => Akka.Cluster.Cluster.Get(Context.System);
        public IActorRef BenchmarkCoordinator {get;}

        public IActorRef RoundHost {get;set;}

        private readonly ILoggingAdapter _log = Context.GetLogger();

        public BenchmarkHost(IActorRef benchmarkCoordinator)
        {
            BenchmarkCoordinator = benchmarkCoordinator;

            NotInRound();
        }

        private void NotInRound(){
            Receive<BenchmarkToNode>(b =>{
                RoundHost = Context.ActorOf(Props.Create(() => new BenchmarkRoundHost(BenchmarkCoordinator)), "round-"+ b.Round);
                Become(InRound);
            });
        }

        private void InRound(){
            Receive<BenchmarkToNode>(b =>{
                _log.Warning("SHOULD NOT HAVE RECEIVED BENCHMARKTONODE - previous round not complete!");
            });

            Receive<RoundComplete>(_ => {
                Context.Stop(RoundHost);
                Become(NotInRound);
            });
        }
    }
}
