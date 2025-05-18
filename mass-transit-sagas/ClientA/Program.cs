using MassTransit;
using Shop;

namespace ClientA
{
    public static class ConsoleHelper
    {
        private static readonly object _lock = new object();
        public static void WriteLocked(ConsoleColor color, string message)
        {

            lock (_lock)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(message);
                Console.ResetColor();
            }
        }
    }

    public class ClientOrderState
    {
        public bool IsOrderInProcess { get; set; } = false;
    }

    class HandlerClientClass :
        IConsumer<AskForClientConfirmation>,
        IConsumer<OrderAccepted>,
        IConsumer<OrderCanceled>
    {
        private readonly ClientOrderState _orderState;

        public HandlerClientClass(ClientOrderState orderState)
        {
            _orderState = orderState;
        }

        public async Task Consume(ConsumeContext<AskForClientConfirmation> context)
        {
            if (context.Message.ClientLogin != "ClientA")
            {
                return;
            }

            ConsoleHelper.WriteLocked(ConsoleColor.Cyan,
                                        $"[ClientA] Do you confirm order #{context.Message.Id} with quantity {context.Message.Quantity}? (y/n)");
            while (_orderState.IsOrderInProcess)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Y)
                    {
                        await context.Publish(new ClientConfirmation
                        {
                            CorrelationId = context.Message.Id
                        });
                        ConsoleHelper.WriteLocked(ConsoleColor.Green,
                            $"[ClientA] Confirmed order #{context.Message.Id}");
                        break;
                    }
                    else if (key.Key == ConsoleKey.N)
                    {
                        await context.Publish(new ClientNoConfirmation
                        {
                            CorrelationId = context.Message.Id
                        });
                        ConsoleHelper.WriteLocked(ConsoleColor.Red,
                            $"[ClientA] Order #{context.Message.Id} not confirmed");
                        break;
                    }
                }
                else
                {
                    await Task.Delay(100);
                }
            }
        }

        public Task Consume(ConsumeContext<OrderAccepted> context)
        {
            if (context.Message.ClientLogin != "ClientA")
            {
                return Task.CompletedTask;
            }

            ConsoleHelper.WriteLocked(ConsoleColor.Green,
                                        $"[ClientA] Order #{context.Message.Id} ACCEPTED by shop for quantity {context.Message.Quantity}. \n Send new order with 's' key");
            _orderState.IsOrderInProcess = false;
            return Task.CompletedTask;
        }

        public Task Consume(ConsumeContext<OrderCanceled> context)
        {
            if (context.Message.ClientLogin != "ClientA")
            {
                return Task.CompletedTask;
            }

            ConsoleHelper.WriteLocked(ConsoleColor.Red,
                                        $"[ClientA] Order #{context.Message.Id} CANCELED by shop for quantity {context.Message.Quantity}. \n Send new order with 's' key");
            _orderState.IsOrderInProcess = false;
            return Task.CompletedTask;
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var clientOrderState = new ClientOrderState();

            var bus = Bus.Factory.CreateUsingRabbitMq(sbc =>
            {
                sbc.Host(new Uri(""), h =>
                {
                    h.Username("");
                    h.Password("");
                });

                sbc.ReceiveEndpoint("clientA", e =>
                {
                    e.Consumer(() => new HandlerClientClass(clientOrderState));
                });
            });

            await bus.StartAsync();
            Console.WriteLine("ClientA started | Press 's' to send order, 'q' to quit...");
            while (true)
            {
                if (!clientOrderState.IsOrderInProcess)
                {
                    var key = Console.ReadKey();
                    if (key.Key == ConsoleKey.S)
                    {

                        Console.WriteLine();
                        Console.Write("Enter quantity: ");

                        int quantity;

                        while (!int.TryParse(Console.ReadLine(), out quantity) || quantity <= 0)
                        {
                            Console.Write("Invalid input. Enter a positive integer for quantity: ");
                        }

                        clientOrderState.IsOrderInProcess = true;

                        await bus.Publish(new StartOrder
                        {
                            Quantity = quantity,
                            ClientLogin = "ClientA"
                        });

                        ConsoleHelper.WriteLocked(ConsoleColor.Cyan,
                                        $"[ClientA] Order with quantity {quantity} sent");
                    }
                    else if (key.Key == ConsoleKey.Q)
                    {
                        break;
                    }
                }
            }
        }
    }
}
