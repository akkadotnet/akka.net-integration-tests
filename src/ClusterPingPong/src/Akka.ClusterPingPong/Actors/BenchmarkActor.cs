using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using static Akka.ClusterPingPong.Messages.BenchmarkProtocol;

namespace Akka.ClusterPingPong.Actors
{
    public class BenchmarkActor : UntypedActor
    {
        private readonly long _maxExpectedMessages;
        private readonly IActorRef _echo;
        private long _currentMessages = 0;

        private readonly Address _pingee;
        private readonly Address _selfAddress;

        private DateTimeOffset _start;

        public BenchmarkActor(long maxExpectedMessages, IActorRef echo, Address pingee, Address selfAddress)
        {
            _maxExpectedMessages = maxExpectedMessages;
            _echo = echo;
            _pingee = pingee;
            _selfAddress = selfAddress;
        }
        protected override void OnReceive(object message)
        {
            switch(message){
                case Begin begin:
                {
                    Context.Become(Running);
                    for (var i = 0; i < 50; i++) // prime the pump so EndpointWriters can take advantage of their batching model
                        Self.Tell("hit");
                    _start = DateTimeOffset.UtcNow;
                    break;
                }
                default:
                    Unhandled(message);
                    break;
            }
        }

        private void Running(object message){
            if (_currentMessages < _maxExpectedMessages)
            {
                _currentMessages++;
                _echo.Tell(message);
            }
            else // reached the end of the benchmark
            {
                var elapsed = DateTimeOffset.UtcNow - _start;
                var roundStats = new RoundStats(){ ReceivedMessages=_currentMessages, Pingee = _pingee, Pinger = _selfAddress, Elapsed = elapsed };
                Context.Parent.Tell(roundStats);
                Context.Stop(Self); // shut ourselves down
            }
        }
    }
}
