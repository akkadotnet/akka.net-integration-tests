using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.PersistentCart.Actors;
using Akka.PersistentCart.Messages;

namespace Akka.PersistentCart
{
    public static class States
    {
        public interface IState
        {
            public Dictionary<ConsoleKey, Func<Task>> Handlers { get; }
            public void PrintMenu();
        }

        public class MainMenu : IState
        {
            public Dictionary<ConsoleKey, Func<Task>> Handlers { get; } = new Dictionary<ConsoleKey, Func<Task>>
            {
                {
                    ConsoleKey.D1, async () =>
                    {
                        Program.StateStack.Push(new EditCart());
                    }
                },
                {
                    ConsoleKey.D2, async () =>
                    {
                        Program.StateStack.Push(new ViewCart());
                    }
                },
                {
                    ConsoleKey.X, async () =>
                    {
                        Program.StateStack.Pop();
                    }
                },
            };

            public void PrintMenu()
            {
                Console.WriteLine(@"
MENU:
1. Create and edit cart.
2. View persisted carts.
X. Exit.");
            }
        }

        public class ViewCart : IState
        {
            private long _currentId = -1;
            private IActorRef _currentCart;

            public ViewCart()
            {
                var task = Program.CounterActor.Ask<long>(PersistentCounterActor.Get.Instance);
                task.Wait();
                var maxId = task.Result;

                if (maxId < 0)
                {
                    Console.WriteLine("No cart is persisted yet.");
                }
                else
                {
                    _currentId = 0;
                    _currentCart = Program.ActorSystem.ActorOf(PersistentCartActor.Props(Program.ReportingActor, _currentId));
                    Console.WriteLine($"Cart loaded. Cart Id: [{_currentId}]");
                }

                Handlers = new Dictionary<ConsoleKey, Func<Task>>
                {
                    {
                        ConsoleKey.D1, async () =>
                        {
                            if (_currentId <= 0)
                            {
                                Console.WriteLine("No more cart before this.");
                                return;
                            }
                            _currentId--;
                            _currentCart = Program.ActorSystem.ActorOf(PersistentCartActor.Props(Program.ReportingActor, _currentId));
                            Console.WriteLine($"Cart loaded. Cart Id: [{_currentId}]");
                        }
                    },
                    {
                        ConsoleKey.D2, async () =>
                        {
                            if (_currentId >= maxId)
                            {
                                Console.WriteLine("No more cart after this.");
                                return;
                            }
                            _currentId++;
                            _currentCart = Program.ActorSystem.ActorOf(PersistentCartActor.Props(Program.ReportingActor, _currentId));
                            Console.WriteLine($"Cart loaded. Cart Id: [{_currentId}]");
                        }
                    },
                    {
                        ConsoleKey.D3, async () =>
                        {
                            if (_currentCart == null)
                            {
                                Console.WriteLine("There is no current cart");
                                return;
                            }

                            var cart = await _currentCart.Ask<ICart>(CartProtocol.GetCurrentCart.Instance, TimeSpan.FromSeconds(3));
                            switch (cart)
                            {
                                case EmptyCart:
                                    Console.WriteLine("Cart is empty.");
                                    break;

                                case FilledCart fc:
                                    Console.WriteLine("Cart content:");
                                    foreach (var item in fc.Items)  
                                    {
                                        Console.WriteLine($"  - [Item] ID: [{item.Id}], Name: [{item.Name}], Price: [{item.Price}]");
                                    }
                                    break;

                                case PaidCart pc:
                                    Console.WriteLine("Paid cart content:");
                                    foreach (var item in pc.Items)  
                                    {
                                        Console.WriteLine($"  - [Item] ID: [{item.Id}], Name: [{item.Name}], Price: [{item.Price}]");
                                    }
                                    break;
                            }
                        }
                    },
                    {
                        ConsoleKey.X, async () =>
                        {
                            _currentCart?.Tell(CartProtocol.Leave.Instance);
                            Program.StateStack.Pop();
                        }
                    },
                };
            }

            public Dictionary<ConsoleKey, Func<Task>> Handlers { get; }
            public void PrintMenu()
            {
                Console.WriteLine(@"
MENU:
1. Previous cart.
2. Next cart.
3. View current cart.
X. Back.");
            }
        }

        public class EditCart : IState
        {
            private IActorRef _currentCart;

            public EditCart()
            {
                Handlers = new Dictionary<ConsoleKey, Func<Task>>
                {
                    {
                        ConsoleKey.D1, async () =>
                        {
                            _currentCart?.Tell(CartProtocol.Leave.Instance);
                            var id = await Program.CounterActor.Ask<long>(PersistentCounterActor.IncrementAndGet.Instance);
                            _currentCart =
                                Program.ActorSystem.ActorOf(PersistentCartActor.Props(Program.ReportingActor, id));
                            Console.WriteLine($"New cart created. Cart Id: [{id}]");
                        }
                    },
                    {
                        ConsoleKey.D2, async () =>
                        {
                            if (_currentCart == null)
                            {
                                Console.WriteLine("There is no current cart");
                                return;
                            }

                            var cart = await _currentCart.Ask<ICart>(CartProtocol.GetCurrentCart.Instance, TimeSpan.FromSeconds(3));
                            switch (cart)
                            {
                                case EmptyCart:
                                    Console.WriteLine("Cart is empty.");
                                    break;

                                case FilledCart fc:
                                    Console.WriteLine("Cart content:");
                                    foreach (var item in fc.Items)  
                                    {
                                        Console.WriteLine($"  - [Item] ID: [{item.Id}], Name: [{item.Name}], Price: [{item.Price}]");
                                    }
                                    break;

                                case PaidCart pc:
                                    Console.WriteLine("Paid cart content:");
                                    foreach (var item in pc.Items)  
                                    {
                                        Console.WriteLine($"  - [Item] ID: [{item.Id}], Name: [{item.Name}], Price: [{item.Price}]");
                                    }
                                    break;
                            }
                        }
                    },
                    {
                        ConsoleKey.D3, async () =>
                        {
                            if(_currentCart == null)
                            {
                                Console.WriteLine("There is no current cart");
                                return;
                            }
                            _currentCart.Tell(new CartProtocol.AddItem(new Item("123", "Item Name", 99.9f)));
                            Console.WriteLine("Message sent.");
                        }
                    },
                    {
                        ConsoleKey.D4, async () =>
                        {
                            _currentCart?.Tell(CartProtocol.Buy.Instance);
                            Console.WriteLine("Message sent.");
                        }
                    },
                    {
                        ConsoleKey.X, async () =>
                        {
                            _currentCart?.Tell(CartProtocol.Leave.Instance);
                            Program.StateStack.Pop();
                        }
                    },
                };
            }

            public Dictionary<ConsoleKey, Func<Task>> Handlers { get; }

            public void PrintMenu()
            {
                Console.WriteLine(@"
MENU:
1. Abandon current cart and create a new cart.
2. View current cart.
3. Add new item.
4. Pay.
X. Back.");
            }
        }

    }

}
