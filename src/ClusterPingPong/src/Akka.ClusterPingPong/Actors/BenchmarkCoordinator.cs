using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster;
using Akka.Event;
using static Akka.Cluster.ClusterEvent;
using static Akka.ClusterPingPong.Messages.BenchmarkProtocol;

namespace Akka.ClusterPingPong.Actors
{
    public class BenchmarkCoordinator : UntypedActor
    {
        protected override void OnReceive(object message){

        }
    }

    // One of these runs on every node, to deploy the relevant actors
    public sealed class BenchmarkRunner : UntypedActor{

        public sealed class RoundStats{
            public (Address pinger, Address pingee) Pair {get;set;}
            public int Actors {get;set;}
            public int ExpectedMessages {get;set;}
            public int ActualMessages {get;set;}
            public TimeSpan Elapsed{get;set;}
        }
        private Dictionary<(Address pinger, Address pingee), List<RoundStats>> Stats = new Dictionary<(Address pinger, Address pingee), List<RoundStats>>();
        private HashSet<Address> _participatingNodes = new HashSet<Address>();
        private readonly ILoggingAdapter _log = Context.GetLogger();

        public Akka.Cluster.Cluster Cluster { get; } = Akka.Cluster.Cluster.Get(Context.System);

        public int MinimumParticipatingNodes { get; }

        public int Rounds {get;}
        private int _currentRound = 0;

        public int ActorsPerRound(int roundNumber){
            return Math.Max(1, roundNumber*5);
        }

        const long MESSAGES_PER_PAIR = 100000L;

        private ICancelable _stableAfterTime = null;

        public BenchmarkRunner(int minParticipants, int rounds){
            MinimumParticipatingNodes = minParticipants;
            Rounds = rounds;
        }

        protected override void OnReceive(object message){
            switch(message)
            {
                case MemberUp up:
                {
                    _participatingNodes.Add(up.Member.Address);
                    _log.Info("Added [{0}] to set of participating nodes...", up.Member);
                    ResetStableTimer();
                    break;
                }
                case MemberRemoved removed:
                {
                    _participatingNodes.Remove(removed.Member.Address);
                    _log.Info("Removed [{0}] to set of participating nodes...", removed.Member);
                    ResetStableTimer();
                    break;
                }
                case IReachabilityEvent _:
                {
                    ResetStableTimer();
                    break;
                }
                case IMemberEvent _:
                {
                    ResetStableTimer();
                    break;
                }
                case ClusterStable _ when _participatingNodes.Count >= MinimumParticipatingNodes:
                {
                    _log.Info("BENCHMARK READY: reached {0} participating nodes.", _participatingNodes.Count);

                    /*
                     * Need to pair off nodes. Cluster with members [A,B,C] should produce
                     * - (A,B)
                     * - (B,C)
                     * - (A,C)
                     */
                    var pairs = _participatingNodes.ToArray();
                    var nodePairs = new List<(Address pinger, Address pingee)>();
                    for (var i = 0; i < pairs.Length; i++)
                    {
                        for (var k = i+1; k < pairs.Length; k++) {
                            nodePairs.Add((pairs[i], pairs[k]));
                        }
                    }
                    
                    // populate our stats table
                    foreach(var p in nodePairs){
                        Stats[p] = new List<RoundStats>();
                    }
                    break;
                }
                case ClusterStable _:
                {
                    _log.Info("BENCHMARK NOT READY: only reached {0} participating nodes. Need {1} to complete. Terminating cluster.", _participatingNodes.Count, MinimumParticipatingNodes);
                    foreach(var d in _participatingNodes){
                        Cluster.Down(d);
                    }
                    break;
                }
                default:
                    Unhandled(message);
                    break;
            }
        }

        protected void BenchmarkRunning(object message){
            
        }

        protected override void PreStart(){
            Cluster.Subscribe(Context.Self, SubscriptionInitialStateMode.InitialStateAsEvents, typeof(ClusterEvent.IMemberEvent), typeof(ClusterEvent.IReachabilityEvent));
        }

        private void ResetStableTimer(){
            _stableAfterTime?.Cancel(); // cancel any previous timer
            _stableAfterTime = Context.System.Scheduler.ScheduleTellOnceCancelable(TimeSpan.FromSeconds(20), Self, ClusterStable.Instance, ActorRefs.NoSender);
        }

        private class ClusterStable{
            public static readonly ClusterStable Instance = new ClusterStable();
            private ClusterStable(){}
        }

        private class StartRound{
            public StartRound(int i){
                Round = i;
            }
            public int Round {get;}
        }
    }
}
