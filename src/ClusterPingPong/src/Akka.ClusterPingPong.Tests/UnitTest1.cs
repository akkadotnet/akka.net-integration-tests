using Akka.Actor;
using Akka.Actor.Dsl;
using Akka.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace Akka.ClusterPingPong.Tests
{
    public class UnitTest1 : TestKit.Xunit2.TestKit
    {
        public static Config GetTestConfig()
        {
            return @"
                akka{

                }
            ";
        }

        public UnitTest1(ITestOutputHelper helper) : base(GetTestConfig(), output: helper)
        {

        }

        [Fact]
        public void TestMethod1()
        {
            var actor = Sys.ActorOf(act =>
            {
                act.ReceiveAny(((o, context) =>
                {
                    context.Sender.Tell(o);
                }));
            });

            actor.Tell("hit");
            ExpectMsg("hit");
        }
    }
}
