using System.Reflection.Metadata;
using MassTransit;
using Shop;

namespace Warehouse
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

    class HandlerWarehouseClass :
        IConsumer<AskForWarehouseConfirmation>,
        IConsumer<OrderAccepted>,
        IConsumer<OrderCanceled>
    {
        private readonly object _lock = new object();
        private int ToBuy { get; set; } = 0;
        private Dictionary<Guid, int> reserved = new Dictionary<Guid, int>();

        public void AddToBuy(int value)
        {
            lock (_lock)
            {
                ToBuy += value;
            }
        }

        public void PrintStock()
        {
            int reservedSum = 0;
            lock (_lock)
            {
                foreach (var item in reserved)
                {
                    reservedSum += item.Value;
                }
            }
            ConsoleHelper.WriteLocked(ConsoleColor.Yellow,
                $"[Warehouse] Stock: {ToBuy} | Reserved: {reservedSum}");
        }

        public Task Consume(ConsumeContext<AskForWarehouseConfirmation> context)
        {
            bool confirmed = false;
            lock (_lock)
            {
                ConsoleHelper.WriteLocked(ConsoleColor.Yellow,
                    $"[Warehouse] Order #{context.Message.Id} for quantity {context.Message.Quantity} is being processed.");
                if (ToBuy >= context.Message.Quantity)
                {
                    ConsoleHelper.WriteLocked(ConsoleColor.Green,
                        $"[Warehouse] Order #{context.Message.Id} is available.");
                    reserved.Add(context.Message.Id, context.Message.Quantity);
                    ToBuy -= context.Message.Quantity;
                    confirmed = true;
                }
                else
                {
                    ConsoleHelper.WriteLocked(ConsoleColor.Red,
                        $"[Warehouse] Order #{context.Message.Id} is NOT available.");
                }
            }
            PrintStock();
            if (confirmed)
            {
                context.Publish(new WarehouseConfirmation
                {
                    CorrelationId = context.Message.Id
                });
            }
            else
            {
                context.Publish(new WarehouseNoConfirmation
                {
                    CorrelationId = context.Message.Id
                });
            }

            return Task.CompletedTask;
        }

        public Task Consume(ConsumeContext<OrderAccepted> context)
        {
            ConsoleHelper.WriteLocked(ConsoleColor.Green,
                $"[Warehouse] Order #{context.Message.Id} ACCEPTED by shop for quantity {context.Message.Quantity}.");
            lock (_lock)
            {
                reserved.Remove(context.Message.Id);
            }
            PrintStock();
            return Task.CompletedTask;
        }

        public Task Consume(ConsumeContext<OrderCanceled> context)
        {
            ConsoleHelper.WriteLocked(ConsoleColor.Red,
                $"[Warehouse] Order #{context.Message.Id} CANCELED by shop for quantity {context.Message.Quantity}.");
            lock (_lock)
            {
                if (reserved.TryGetValue(context.Message.Id, out var toAdd))
                {
                    ToBuy += toAdd;
                    reserved.Remove(context.Message.Id);
                }
            }
            PrintStock();
            return Task.CompletedTask;
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var Warehouse = new HandlerWarehouseClass();
            var bus = Bus.Factory.CreateUsingRabbitMq(sbc =>
            {
                sbc.Host(new Uri(""), h =>
                {
                    h.Username("");
                    h.Password("");
                });

                sbc.ReceiveEndpoint("Warehouse", e =>
                {
                    e.Instance(Warehouse);
                });
            });

            await bus.StartAsync();
            Console.WriteLine("Warehouse started | To add quantity of goods enter the number, 'q' to quit...");
            while (true)
            {
                Console.Write("Enter quantity to add (or 'q' to quit): ");
                var input = Console.ReadLine();
                if (input != null && input.Trim().ToLower() == "q")
                {
                    break;
                }
                if (int.TryParse(input, out int quantity) && quantity > 0)
                {
                    Warehouse.AddToBuy(quantity);
                    Warehouse.PrintStock();
                }
                else
                {
                    Console.WriteLine("Invalid input. Please enter a positive integer or 'q' to quit.");
                }
            }
        }
    }
}
