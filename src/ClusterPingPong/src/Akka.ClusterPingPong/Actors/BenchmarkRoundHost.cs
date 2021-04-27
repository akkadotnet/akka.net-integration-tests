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

        private readonly ILoggingAdapter _log = Context.GetLogger();

        public BenchmarkRoundHost(IActorRef benchmarkCoordinator)
        {
            BenchmarkCoordinator = benchmarkCoordinator;

            Waiting();
        }

        private void Waiting(){
             ReceiveAsync<BenchmarkToNode>(async b =>{
                ExpectedMessages = b.ExpectedMessages;
                ExpectedActors = b.Actors;

                // start up the EchoActors if we're the recipient
                if(b.Pingee.Equals(Cluster.SelfAddress)){
                    foreach(var i in Enumerable.Range(0, ExpectedActors)){
                        var echo = Context.ActorOf(EchoActor.EchoProps, "echo-"+i);
                        _currentRoundEchoActors.Add(echo);
                    }

                    var remoteHost = Context.ActorSelection(b.Pinger + "/user/host");
                    var remoteHostActor = await remoteHost.ResolveOne(TimeSpan.FromSeconds(3));

                    remoteHostActor.Tell(new PingeeAck(){ Pingee = Cluster.SelfAddress, EchoActors = _currentRoundEchoActors.ToArray() });
                }                

                Become(Starting);
            });
        }

        private void Starting(){
            Receive<BenchmarkToNode>(b =>{
                _log.Warning("SHOULD NOT HAVE RECEIVED BENCHMARKTONODE - previous round not complete!");
            });

            Receive<PingeeAck>(ack => {
                var i = 0;
                var selfAddress = Cluster.SelfAddress;
                foreach(var echo in ack.EchoActors){
                    var benchmarkActor = Context.ActorOf(Props.Create(() => new BenchmarkActor(ExpectedMessages, echo, ack.Pingee, selfAddress)), "benchmark-"+ i++);
                    _currentRoundBenchmarkActors.Add(benchmarkActor);
                }

                // report back to coordinator that we're ready to begin
                BenchmarkCoordinator.Tell(new NodeReady(){ Pingee = ack.Pingee, Pinger = selfAddress, BenchmarkHost = Self });
            });

            Receive<Begin>(begin => {
                foreach(var b in _currentRoundBenchmarkActors){
                    b.Forward(begin);
                }
                Become(Running);
            });

        }

        private void Running(){
            Receive<BenchmarkToNode>(b =>{
                _log.Warning("SHOULD NOT HAVE RECEIVED BENCHMARKTONODE - previous round not complete!");
            });

            Receive<RoundStats>(r => {
                Stats.Add(r);
                if(Stats.Count == _currentRoundBenchmarkActors.Count){ // all data collected
                    var merged = Stats.Aggregate(Stats[0], (stats, rounds) => {
                        return stats = stats with { ReceivedMessages = stats.ReceivedMessages + rounds.ReceivedMessages, 
                            Elapsed = new TimeSpan(Math.Max(stats.Elapsed.Ticks, rounds.Elapsed.Ticks)) };
                    });
                    
                    // send merged stats to the BenchmarkCoordinator
                    BenchmarkCoordinator.Tell(merged);
                }
            });
        }
    }
}
