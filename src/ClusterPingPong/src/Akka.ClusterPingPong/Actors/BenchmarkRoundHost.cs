using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using static Akka.ClusterPingPong.Messages.BenchmarkProtocol;

namespace Akka.ClusterPingPong.Actors
{
    // We create one of these per node pair.
    public class BenchmarkRoundHost : ReceiveActor
    {
        public Akka.Cluster.Cluster Cluster => Akka.Cluster.Cluster.Get(Context.System);
        public IActorRef BenchmarkCoordinator {get;}

        private HashSet<IActorRef> _currentRoundEchoActors = new HashSet<IActorRef>();

        private HashSet<IActorRef> _currentRoundBenchmarkActors = new HashSet<IActorRef>();
        
        public int ExpectedMessages {get;set;}

        public int ExpectedActors {get;set;}

        // All of the completed stats from each successive rounds
        public List<RoundStats> Stats = new List<RoundStats>();

        public BenchmarkRoundHost(IActorRef benchmarkCoordinator)
        {
            BenchmarkCoordinator = benchmarkCoordinator;

            Receive<BenchmarkToNode>(b =>{
                ExpectedMessages = b.ExpectedMessages;
                ExpectedActors = b.ExepectedActors;
                foreach(var i in Enumerable.Range(0, ExpectedActors)){
                    var echo = Context.ActorOf(EchoActor.EchoProps, "echo-"+i);
                    _currentRoundEchoActors.Add(echo);
                }

                // TODO: send PingeeAck to remote node
            });

            Receive<PingeeAck>(ack => {
                var i = 0;
                foreach(var echo in ack.EchoActors){
                    var benchmarkActor = Context.ActorOf(Props.Create(() => new BenchmarkActor(ExpectedMessages, echo)), "benchmark-"+ i++);
                    _currentRoundBenchmarkActors.Add(benchmarkActor);
                }
            });

            Receive<RoundStats>(r => {
                
            });
        }
    }
}
