using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;

namespace Akka.ClusterPingPong.Messages
{
    public static class BenchmarkProtocol
    {
        public interface IBenchmarkMsg{}

        // Used to tell two nodes the establish a ping / pong pair
        public record BenchmarkToNode : IBenchmarkMsg
        {
            public Address Pinger { get; set; }

            public Address Pingee { get; set; }
        }

        // EchoActor is ready
        public record PingeeAck : IBenchmarkMsg{
            public IActorRef EchoActor { get; set;}
        }

        // BenchmarkActor is ready and has received reference to EchoActor
        public record PingerAck : IBenchmarkMsg{
            public IActorRef EchoActor { get; set;}

            public IActorRef BenchmarkActor {get;set;}
        }

        // Signal to BenchmarkActor to begin
        public record Begin : IBenchmarkMsg{
            public long ExpectedMessages { get; set;}
        }

        // Signal to BenchmarkCoordinator that one ping-pong leg has completed
        public record RoundStats : IBenchmarkMsg{
            public long ReceivedMessges { get; set;}

            public TimeSpan Elapsed { get; set;}

            public Address Pinger { get; set; }

            public Address Pingee { get; set; }

            public override string ToString() => $"BenchmarkRun(From={Pinger},To={Pingee})[MessagesRecv:{ReceivedMessages}][Elapsed:{Elapsed}]";
        }

        // Signals the end of the round
        public sealed class RoundComplete : IBenchmarkMsg{ }
    }
}
