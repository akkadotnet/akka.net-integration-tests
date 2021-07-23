using System;
using System.Collections.Immutable;
using Akka.Actor;
using Akka.Persistence.Fsm;
using Akka.PersistentCart.Messages;

namespace Akka.PersistentCart.Actors
{
    #region States

    public sealed class LookingAround : PersistentFSM.IFsmState
    {
        public static LookingAround Instance = new LookingAround();

        public string Identifier { get; } = nameof(LookingAround);

        public override bool Equals(object obj) => obj is LookingAround;

        public override int GetHashCode()
            => Identifier.GetHashCode();
    }

    public sealed class Shopping : PersistentFSM.IFsmState
    {
        public static Shopping Instance = new Shopping();

        public string Identifier { get; } = nameof(Shopping);

        public override bool Equals(object obj) => obj is Shopping;

        public override int GetHashCode()
            => Identifier.GetHashCode();
    }

    public sealed class Inactive : PersistentFSM.IFsmState
    {
        public static Inactive Instance = new Inactive();

        public string Identifier { get; } = nameof(Inactive);

        public override bool Equals(object obj) => obj is Inactive;

        public override int GetHashCode()
            => Identifier.GetHashCode();
    }

    public sealed class Paid : PersistentFSM.IFsmState
    {
        public static Paid Instance = new Paid();

        public string Identifier { get; } = nameof(Paid);

        public override bool Equals(object obj) => obj is Paid;

        public override int GetHashCode()
            => Identifier.GetHashCode();
    }

    #endregion

    #region Data

    public interface ICart
    {
        ICart AddItem(Item item);
        ICart Pay();
        ICart Empty();
    }

    public sealed class EmptyCart : ICart
    {
        public static EmptyCart Instance = new EmptyCart();

        public ICart AddItem(Item item) => new FilledCart(ImmutableList<Item>.Empty).AddItem(item);

        public ICart Pay() => this;

        public ICart Empty() => this;
    }

    public sealed class FilledCart : ICart
    {
        public FilledCart(ImmutableList<Item> items)
        {
            Items = items;
        }

        public ImmutableList<Item> Items { get; }

        public ICart AddItem(Item item) => new FilledCart(Items.Add(item));

        public ICart Pay() => new PaidCart(Items);

        public ICart Empty() => EmptyCart.Instance;
    }

    public sealed class PaidCart : ICart
    {
        public PaidCart(ImmutableList<Item> items)
        {
            Items = items;
        }

        public ImmutableList<Item> Items { get; }

        public ICart AddItem(Item item) => this;

        public ICart Pay() => this;

        public ICart Empty() => this;
    }

    #endregion

    public class PersistentCartActor : PersistentFSM<PersistentFSM.IFsmState, ICart, CartProtocol.ICommand>
    {
        public static Props Props(IActorRef reportActor, long id) => Actor.Props.Create<PersistentCartActor>(reportActor, id);

        private readonly long _id;

        public override string PersistenceId => $"shoppingCart_{_id}";

        public PersistentCartActor(IActorRef reportActor, long id)
        {
            _id = id;

            StartWith(LookingAround.Instance, EmptyCart.Instance);

            When(LookingAround.Instance, (evt, _) =>
            {
                return evt.FsmEvent switch
                {
                    CartProtocol.AddItem msg => GoTo(Shopping.Instance)
                        .Applying(new CartProtocol.ItemAdded(msg.Item))
                        .ForMax(TimeSpan.FromSeconds(10)),

                    CartProtocol.GetCurrentCart => Stay().Replying(evt.StateData),

                    _ => Stay()
                };
            });

            When(Shopping.Instance, (evt, _) =>
            {
                return evt.FsmEvent switch
                {
                    CartProtocol.AddItem msg => Stay()
                        .Applying(new CartProtocol.ItemAdded(msg.Item))
                        .AndThen(cart => SaveStateSnapshot())
                        .ForMax(TimeSpan.FromSeconds(1)),

                    CartProtocol.Buy => GoTo(Paid.Instance)
                        .Applying(CartProtocol.OrderExecuted.Instance)
                        .AndThen(cart =>
                        {
                            if (cart is FilledCart filledCart)
                            {
                                reportActor.Tell(new CartProtocol.PurchaseWasMade(filledCart.Items));
                                SaveStateSnapshot();
                            }
                        }),

                    CartProtocol.Leave => Stop()
                        .Applying(CartProtocol.OrderDiscarded.Instance)
                        .AndThen(_ =>
                        {
                            reportActor.Tell(CartProtocol.CartDiscarded.Instance);
                            SaveStateSnapshot();
                        }),

                    CartProtocol.GetCurrentCart 
                        => Stay().Replying(evt.StateData),

                    FSMBase.StateTimeout => GoTo(Inactive.Instance),

                    _ => Stay()
                };
            });

            When(Inactive.Instance, (evt, _) =>
            {
                return evt.FsmEvent switch
                {
                    CartProtocol.AddItem msg => GoTo(Shopping.Instance)
                        .Applying(new CartProtocol.ItemAdded(msg.Item))
                        .AndThen(cart => SaveStateSnapshot())
                        .ForMax(TimeSpan.FromSeconds(1)),

                    CartProtocol.Buy => GoTo(Paid.Instance)
                        .Applying(CartProtocol.OrderExecuted.Instance)
                        .AndThen(cart =>
                        {
                            if (cart is FilledCart filledCart)
                            {
                                reportActor.Tell(new CartProtocol.PurchaseWasMade(filledCart.Items));
                                SaveStateSnapshot();
                            }
                        }),

                    CartProtocol.Leave => Stop()
                        .Applying(CartProtocol.OrderDiscarded.Instance)
                        .AndThen(_ =>
                        {
                            reportActor.Tell(CartProtocol.CartDiscarded.Instance);
                            SaveStateSnapshot();
                        }),

                    CartProtocol.GetCurrentCart => GoTo(Shopping.Instance)
                        .Replying(evt.StateData),

                    _ => Stay()
                };
            });

            When(Paid.Instance, (evt, _) =>
            {
                return evt.FsmEvent switch
                {
                    CartProtocol.Leave => Stop(),

                    CartProtocol.GetCurrentCart => Stay().Replying(evt.StateData),

                    _ => Stay()
                };
            });
        }

        protected override ICart ApplyEvent(CartProtocol.ICommand domainEvent, ICart currentData)
        {
            return domainEvent switch
            {
                CartProtocol.ItemAdded msg => currentData.AddItem(msg.Item),
                CartProtocol.OrderExecuted => currentData.Pay(),
                CartProtocol.OrderDiscarded => currentData,
                CartProtocol.CustomerInactive => currentData,
                _ => currentData
            };
        }
    }
}
