using Akka.Actor;

namespace Akka.ClusterPingPong.Actors
{
    public class EchoActor : UntypedActor
    {
        protected override void OnReceive(object message)
        {
            Sender.Tell(message);
        }
    }
}