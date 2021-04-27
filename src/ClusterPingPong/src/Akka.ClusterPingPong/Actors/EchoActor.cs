using Akka.Actor;

namespace Akka.ClusterPingPong.Actors
{
    public class EchoActor : UntypedActor
    {
        protected override void OnReceive(object message)
        {
            Sender.Tell(message);
        }

        public static Props EchoProps {get;} = Props.Create(() => new EchoActor());
    }
}