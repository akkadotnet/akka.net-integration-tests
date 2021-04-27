using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;

namespace Akka.ClusterPingPong.Actors
{
    public class BenchmarkActor : UntypedActor
    {
        private readonly long _maxExpectedMessages;
        private readonly IActorRef _echo;
        private long _currentMessages = 0;
        private readonly TaskCompletionSource<long> _completion;

        public BenchmarkActor(long maxExpectedMessages, IActorRef echo)
        {
            _maxExpectedMessages = maxExpectedMessages;
            _completion = completion;
            _echo = echo;
        }
        protected override void OnReceive(object message)
        {
            if (_currentMessages < _maxExpectedMessages)
            {
                _currentMessages++;
                _echo.Tell(message);
            }
            else
            {
                
            }
        }

        protected override void PreStart(){

        }
    }
}
