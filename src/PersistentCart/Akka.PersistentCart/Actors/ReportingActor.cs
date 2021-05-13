using Akka.Actor;
using Akka.Event;
using Akka.PersistentCart.Messages;

namespace Akka.PersistentCart.Actors
{
    public class ReportingActor : ReceiveActor
    {
        public ReportingActor()
        {
            var log = Context.GetLogger();

            Receive<CartProtocol.PurchaseWasMade>(msg =>
            {
                log.Info($"Purchase was made. Cart content: [{string.Join(",", msg.Items)}]");
            });

            Receive<CartProtocol.CartDiscarded>(msg =>
            {
                log.Info("Cart was discarded.");
            });
        }
    }
}
