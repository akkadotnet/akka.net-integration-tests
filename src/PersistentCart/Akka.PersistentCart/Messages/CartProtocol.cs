using System.Collections.Immutable;

namespace Akka.PersistentCart.Messages
{
    public sealed class Item
    {
        public Item(string id, string name, float price)
        {
            Id = id;
            Name = name;
            Price = price;
        }

        public string Id { get;}
        public string Name { get; }
        public float Price { get; }
    }

    public static class CartProtocol
    {
        // Command messages
        public interface ICommand { }

        public sealed class AddItem: ICommand
        {
            public AddItem(Item item)
            {
                Item = item;
            }

            public Item Item { get; }
        }

        public sealed class Buy: ICommand
        {
            public static Buy Instance { get; } = new Buy();
            private Buy() { }
        }

        public sealed class Leave: ICommand
        {
            public static Leave Instance { get; } = new Leave();
            private Leave() { }
        }

        public sealed class GetCurrentCart: ICommand
        {
            public static GetCurrentCart Instance { get; } = new GetCurrentCart();
            private GetCurrentCart() { }
        }

        // Domain messages
        public interface IDomainEvent: ICommand { }

        public sealed class ItemAdded: IDomainEvent
        {
            public ItemAdded(Item item)
            {
                Item = item;
            }

            public Item Item { get;}
        }

        public sealed class OrderExecuted : IDomainEvent
        {
            public static OrderExecuted Instance = new OrderExecuted();
            private OrderExecuted() { }
        }

        public sealed class OrderDiscarded : IDomainEvent
        {
            public static OrderDiscarded Instance = new OrderDiscarded();
            private OrderDiscarded() { }
        }

        public sealed class CustomerInactive : IDomainEvent
        {
            public static CustomerInactive Instance = new CustomerInactive();
            private CustomerInactive() { }
        }

        // Reporting messages
        public sealed class PurchaseWasMade
        {
            public PurchaseWasMade(ImmutableList<Item> items)
            {
                Items = items;
            }

            public ImmutableList<Item> Items { get; }
        }

        public sealed class CartDiscarded
        {
            public static CartDiscarded Instance = new CartDiscarded();
            private CartDiscarded() { }
        }
    }
}
