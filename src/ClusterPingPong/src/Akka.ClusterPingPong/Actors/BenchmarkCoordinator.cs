﻿using System;
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
{    // One of these runs on every node, to deploy the relevant actors
    public sealed class BenchmarkCoordinator : UntypedActor{

        public sealed class RoundTotals{
            public (Address pinger, Address pingee) Pair {get;set;}
            public int Actors {get;set;}
            public int ExpectedMessages {get;set;}
            public int ActualMessages {get;set;}
            public TimeSpan Elapsed{get;set;}
        }

        private Dictionary<(Address pinger, Address pingee), RoundStats> Stats = new Dictionary<(Address pinger, Address pingee), RoundStats>();
        private Dictionary<(Address pinger, Address pingee), bool> Ready = new Dictionary<(Address pinger, Address pingee), bool>();
        private HashSet<Address> _participatingNodes = new HashSet<Address>();
        private readonly ILoggingAdapter _log = Context.GetLogger();

        public Akka.Cluster.Cluster Cluster { get; } = Akka.Cluster.Cluster.Get(Context.System);

        public int MinimumParticipatingNodes { get; }

        public int Rounds {get;}
        private int _currentRound = 1;

        // Router used for communicating with all other benchmark hosts
        public IActorRef BenchmarkHostRouter {get;}

        public int ActorsPerRound(int roundNumber){
            return Math.Max(1, roundNumber*5);
        }

        const int MESSAGES_PER_PAIR = 100000;

        private ICancelable _stableAfterTime = null;

        public BenchmarkCoordinator(int minParticipants, int rounds, IActorRef benchmarkHostRouter){
            MinimumParticipatingNodes = minParticipants;
            Rounds = rounds;
            BenchmarkHostRouter = benchmarkHostRouter;
        }

private void ResetStats((Address pinger, Address pingee) p){
     Stats[p] = new RoundStats(){  };
                        Ready[p] = false;
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
                       
                    }
                    Self.Tell(new StartRound(_currentRound));
                    Context.Become(BenchmarkStarting);
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

        private void BenchmarkStarting(object message){
            switch(message){
                case StartRound sr:
                {
                    foreach(var pair in Stats.Keys){
                        var benchmarkToNode = new BenchmarkToNode(){ 
                            Round = sr.Round, 
                            Pingee = pair.pingee, 
                            Pinger = pair.pinger, 
                            ExpectedMessages = MESSAGES_PER_PAIR,
                            Actors = ActorsPerRound(sr.Round)
                        };
                        var timeout = TimeSpan.FromSeconds(3);
                        var self = Self;
                        Context.ActorSelection(pair.pinger + "/user/host").Ask<NodeAck>(benchmarkToNode, timeout)
                        .PipeTo(self, success: (na) => (na, benchmarkToNode));
                    }
                    break;
                }
                case (NodeAck _, BenchmarkToNode bench): // pinger side successfully started
                {
                    // start pingee side
                    var timeout = TimeSpan.FromSeconds(3); 
                    var self = Self;
                     Context.ActorSelection(bench.Pingee + "/user/host").Ask<NodeAck>(bench, timeout)
                        .PipeTo(self, success: (na) => (na, bench, true));
                    break;
                }
                case (NodeAck _, BenchmarkToNode benchmark, bool ready): // both sides ready
                {
                    // ignore - need to wait for NodeReady
                    break;
                }
                case NodeReady ready:
                {
                    var pair = (ready.Pinger, ready.Pingee);
                    Ready[pair] = true;
                    if(Ready.Values.All(x => x)){ // are all nodes ready?

                        Become(Running);
                    }
                    break;
                }
                case Status.Failure failure when _currentRound == 1:
                {
                    // if we fail during the first round, might be a cluster formation problem.
                    // let's rebuild the entire graph from scratch again

                    _log.Error(failure.Cause, "failed to initialize first benchmark. Restarting process...");
                    BenchmarkHostRouter.Tell(new RoundComplete()); // re-initialize all of the BenchmarkHosts
                    throw new ApplicationException("restarting...");
                }
                case Status.Failure failure:
                {
                    // if we fail in a later round, we assume it's a latency / timeout problem
                    // and simply retry re-creating the round.

                    _log.Error(failure.Cause, $"failed to initialize benchmark round [{_currentRound}]. Restarting...");
                    BenchmarkHostRouter.Tell(new RoundComplete()); // re-initialize all of the BenchmarkHosts
                    Self.Tell(new StartRound(_currentRound)); // restart the current round
                    break;
                }
            }
        }

        private void Running(object message){
            switch(message){
                case RoundStats complete:
                {
                    var pair = (complete.Pinger, complete.Pingee);
                    Stats[pair].Add(rou)
                    break;
                }
            }
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
