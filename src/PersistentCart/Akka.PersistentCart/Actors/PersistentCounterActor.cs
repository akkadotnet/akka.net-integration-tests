using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Persistence;
using Akka.Util.Internal;

namespace Akka.PersistentCart.Actors
{
    public class PersistentCounterActor : PersistentActor
    {
        public sealed class Get
        {
            public static Get Instance { get; } = new Get();
            private Get() { }
        }

        public sealed class GetAndIncrement
        {
            public static GetAndIncrement Instance { get; } = new GetAndIncrement();
            private GetAndIncrement() { }
        }

        public sealed class IncrementAndGet
        {
            public static IncrementAndGet Instance { get; } = new IncrementAndGet();
            private IncrementAndGet() { }
        }

        private readonly AtomicCounterLong _counter = new AtomicCounterLong();
        private bool _initialized = false;

        protected override bool ReceiveRecover(object message)
        {
            switch (message)
            {
                case SnapshotOffer offer:
                    _counter.GetAndSet((long) offer.Snapshot);
                    return true;

                case GetAndIncrement _:
                    _counter.GetAndIncrement();
                    return true;

                case IncrementAndGet _:
                    _counter.IncrementAndGet();
                    return true;

                case RecoveryCompleted _:
                    _initialized = true;
                    return true;

                default:
                    return false;
            }
        }

        protected override bool ReceiveCommand(object message)
        {
            switch (message)
            {
                case GetAndIncrement _:
                    if (!_initialized)
                    {
                        Sender.Tell(-999L, Self);
                        return true;
                    }

                    Persist(message, o =>
                    {
                        var value = _counter.GetAndIncrement();
                        Sender.Tell(value, Self);
                        SaveSnapshot(value);
                    });
                    return true;

                case IncrementAndGet _:
                    if (!_initialized)
                    {
                        Sender.Tell(-999L, Self);
                        return true;
                    }

                    Persist(message, o =>
                    {
                        var value = _counter.IncrementAndGet();
                        Sender.Tell(value, Self);
                        SaveSnapshot(value);
                    });
                    return true;

                case Get _:
                    if (!_initialized)
                    {
                        Sender.Tell(-999L, Self);
                        return true;
                    }

                    Sender.Tell(_counter.Current, Self);
                    return true;
                default:
                    return false;
            }
        }

        public override string PersistenceId { get; } = nameof(PersistentCounterActor);
    }
}
