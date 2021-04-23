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

        public record BenchmarkToNode : IBenchmarkMsg
        {
            public Address Pinger { get; set; }

            public Address Pingee { get; set; }
        }
    }
}
