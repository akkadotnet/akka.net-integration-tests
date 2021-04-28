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
        public IActorRef BenchmarkCoordinator { get; }

        private HashSet<IActorRef> _currentRoundBenchmarkActors = new HashSet<IActorRef>();

        public int ExpectedMessages { get; set; }

        public int ExpectedActors { get; set; }

        // All of the completed stats from each successive rounds
        private readonly Dictionary<(Address pinger, Address pingee), List<RoundStats>> Stats = new Dictionary<(Address pinger, Address pingee), List<RoundStats>>();

        private readonly ILoggingAdapter _log = Context.GetLogger();

        public BenchmarkRoundHost(IActorRef benchmarkCoordinator)
        {
            BenchmarkCoordinator = benchmarkCoordinator;

            Starting();
        }

        private void Starting()
        {
            ReceiveAsync<BenchmarkToNode>(async b =>
            {
                if(_log.IsDebugEnabled)
                    _log.Debug("Starting benchmark with [{0}]->[{1}]", b.Pinger, b.Pingee);
                ExpectedMessages = b.ExpectedMessages;
                ExpectedActors = b.Actors;

                // start up the EchoActors if we're the recipient

                if (b.Pingee.Equals(Cluster.SelfAddress))
                {
                    var roundEchoActors = new List<IActorRef>(ExpectedActors);
                    foreach (var i in Enumerable.Range(0, ExpectedActors))
                    {
                        var echo = Context.ActorOf(EchoActor.EchoProps);
                        roundEchoActors.Add(echo);
                    }

                    var remoteHost = Context.ActorSelection(b.Pinger + "/user/host");
                    var remoteHostActor = await remoteHost.ResolveOne(TimeSpan.FromSeconds(3));

                    var ack = new PingeeAck() { Pingee = Cluster.SelfAddress, EchoActors = roundEchoActors.ToArray() };
                    remoteHostActor.Tell(ack);
                }

                // let the coordinator know that we've started
                Sender.Tell(new NodeAck());
            });

            Receive<PingeeAck>(ack =>
            {
                var selfAddress = Cluster.SelfAddress;
                foreach (var echo in ack.EchoActors)
                {
                    var benchmarkActor = Context.ActorOf(Props.Create(() => new BenchmarkActor(ExpectedMessages, echo, ack.Pingee, selfAddress)));
                    _currentRoundBenchmarkActors.Add(benchmarkActor);
                }

                if (_log.IsDebugEnabled)
                    _log.Debug("Node ready.");

                // report back to coordinator that we're ready to begin
                BenchmarkCoordinator.Tell(new NodeReady() { Pingee = ack.Pingee, Pinger = selfAddress, BenchmarkHost = Self });
            });

            Receive<Begin>(begin =>
            {
                foreach (var b in _currentRoundBenchmarkActors)
                {
                    b.Forward(begin);
                }
                Become(Running);
            });

        }

        private void Running()
        {
            Receive<BenchmarkToNode>(b =>
            {
                _log.Warning("SHOULD NOT HAVE RECEIVED BENCHMARKTONODE - previous round not complete!");
            });

            Receive<RoundStats>(r =>
            {
                var pair = (r.Pinger, r.Pingee);
                if (!Stats.ContainsKey(pair))
                {
                    Stats[pair] = new List<RoundStats>();
                }
                Stats[pair].Add(r);

                var currentTotal = Stats.Sum(x => x.Value.Count);
                if (_log.IsDebugEnabled)
                    _log.Debug("Collected stats - {0} out of {1}", currentTotal, _currentRoundBenchmarkActors.Count);
                if (currentTotal == _currentRoundBenchmarkActors.Count)
                { // all data collected
                    {
                        foreach (var s in Stats.Values)
                        {
                            var emptystats = new RoundStats(){ Pingee = s[0].Pingee, Pinger = s[0].Pinger };
                            // need to merge data for all distinct pairs together
                            var merged = s.Aggregate(emptystats, (s1, rounds) =>
                            {
                                return s1 = s1 with
                                {
                                    ReceivedMessages = s1.ReceivedMessages + rounds.ReceivedMessages,
                                    Elapsed = new TimeSpan(Math.Max(s1.Elapsed.Ticks, rounds.Elapsed.Ticks))
                                };
                            });

                            // send merged stats to the BenchmarkCoordinator
                            BenchmarkCoordinator.Tell(merged);
                        }

                    }

                }
            });
        }
    }
}
