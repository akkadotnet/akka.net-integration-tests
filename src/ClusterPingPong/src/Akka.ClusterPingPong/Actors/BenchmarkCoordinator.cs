using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime;
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

        private Dictionary<(Address pinger, Address pingee), (RoundStats stats, bool complete)> Stats = new Dictionary<(Address pinger, Address pingee), (RoundStats stats, bool complete)>();
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
            Stats[p] = (new RoundStats() { }, false);
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
                       ResetStats(p);
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
                        Context.ActorSelection(new RootActorPath(pair.pinger) / "user" / "host").Ask<NodeAck>(benchmarkToNode, timeout)
                        .PipeTo(self, success: (na) => (na, benchmarkToNode), failure: ex => new Status.Failure(ex));
                    }
                    break;
                }
                case (NodeAck _, BenchmarkToNode bench): // pinger side successfully started
                {
                    // start pingee side
                    var timeout = TimeSpan.FromSeconds(3); 
                    var self = Self;
                     Context.ActorSelection(new RootActorPath(bench.Pingee) / "user" / "host").Ask<NodeAck>(bench, timeout)
                        .PipeTo(self, success: (na) => (na, bench, true), failure: ex => new Status.Failure(ex));
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
                        if(_currentRound == 1){    
                            PrintSysInfo();
                        }
                        Become(Running);
                        BenchmarkHostRouter.Tell(new Begin()); // launch benchmark
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
                default:
                    Unhandled(message);
                    break;
            }
        }

        private void Running(object message){
            switch(message){
                case RoundStats complete:
                {
                    var pair = (complete.Pinger, complete.Pingee);
                    Stats[pair] = (complete, true);

                    // received all stats?
                    if (Stats.Values.All(x => x.complete))
                    {
                        Self.Tell(PrintRound.Instance);
                    }
                    break;
                }
                case PrintRound _:
                {
                    // Console.WriteLine("Nodes, Actors/node, Total [actor], Total [msg], Msgs/sec, Total [ms]");
                    var nodes = Stats.Count;
                    var actors = ActorsPerRound(_currentRound);
                    var total = actors * nodes * 2;
                    // multiply times 2, one for each half of the round
                    var totalMsg = Stats.Sum(x => x.Value.stats.ReceivedMessages) * 2;
                    var avgDuration = new TimeSpan((long)Stats.Average(x => x.Value.stats.Elapsed.Ticks));
                    var msgS = (long)(totalMsg / avgDuration.TotalSeconds);

                    // "Connections, Actors/node, Total [actor], Total [msg], Msgs/sec, Total [ms]"
                    Console.WriteLine("{0, 8}, {1,8}, {2,8}, {3,10:N0}, {4,10:N0}, {5,10}", nodes, actors, total, totalMsg, msgS, avgDuration.TotalMilliseconds.ToString("F2", CultureInfo.InvariantCulture));

                    BenchmarkHostRouter.Tell(new RoundComplete());
                    _currentRound++;
                    if (_currentRound > Rounds)
                    {
                        Console.WriteLine("Benchmark complete");
                        foreach (var node in _participatingNodes)
                        {
                            Cluster.Down(node);
                        }
                    }
                    else
                    {
                        Self.Tell(new StartRound(_currentRound));
                        Context.Become(BenchmarkStarting);
                        foreach (var pair in Stats.Keys)
                        {
                            // reset all stats and readiness values
                            ResetStats(pair);
                        }
                    }
                    break;
                }
                default:
                    Unhandled(message);
                    break;
            }
        }

        private void PrintSysInfo()
        {
            var processorCount = Environment.ProcessorCount;
            if (processorCount == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Failed to read processor count..");
                return;
            }

            Console.WriteLine("OSVersion:                         {0}", Environment.OSVersion);
            Console.WriteLine("ProcessorCount:                    {0}", processorCount);
            Console.WriteLine("Actor Count:                       {0}", processorCount * 2);
            Console.WriteLine("Node Count:                        {0}", _participatingNodes.Count);
            Console.WriteLine("Active Connections:                {0}", Stats.Count);
            Console.WriteLine("Msgs sent/received per connection: {0}  ({0:0e0})", MESSAGES_PER_PAIR * 2);
            Console.WriteLine("Is Server GC:                      {0}", GCSettings.IsServerGC);
            Console.WriteLine("Thread count per node:             {0}", Process.GetCurrentProcess().Threads.Count);
            Console.WriteLine();

            //Print tables
            Console.WriteLine("Connections, Actors/node, Total [actor], Total [msg], Msgs/sec, Total [ms]");
        }

        protected override void PreStart(){
            Cluster.Subscribe(Context.Self, SubscriptionInitialStateMode.InitialStateAsEvents, typeof(ClusterEvent.IMemberEvent), typeof(ClusterEvent.IReachabilityEvent));
            _log.Info("BenchmarkCoordinator started.");
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

        private class PrintRound{
            public static readonly PrintRound Instance = new PrintRound();
            private PrintRound(){}
        }
    }
}
