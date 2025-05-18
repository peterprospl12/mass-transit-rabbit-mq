using MassTransit;
using System;
using System.Threading.Tasks;
using Zad1;

namespace ReceiverA
{
    public class Consumer : IConsumer<Zad1.Publ>
    {
        private int divisbleBy;
        private string name;
        private ConsoleColor color;
        private static readonly object _consoleLock = new object();
        private static readonly Random _rnd = new Random();


        public Task Consume(ConsumeContext<Zad1.Publ> context)
        {
            var number = context.Message.Number;
            if (_rnd.Next(3) == 0)
            {
                lock (_consoleLock)
                {
                    var originalColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[Error] {name} with #{number}");
                    Console.ForegroundColor = originalColor;
                }
                throw new Exception($"[{name}] Random error durring processing #{number}");
            }

            bool shouldResponse = false;
            lock (_consoleLock)
            {
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine($"[{name}] Received message with number: {number}");
                if (number % divisbleBy == 0)
                {
                    shouldResponse = true;
                    Console.WriteLine($"[{name}] Message divisible by {divisbleBy}, responding...");
                }
            }

            if (shouldResponse)
            {
                if (divisbleBy == 2)
                {
                    return context.Publish<ResponseA>(new ResponseA { message = name });
                }
                return context.Publish<ResponseB>(new ResponseB { message = name });
            }

            return Task.CompletedTask;
        }

        Consumer(string nameArg, int divisbleByArg, ConsoleColor colorArg)
        {
            name = nameArg;
            divisbleBy = divisbleByArg;
            color = colorArg;
        }

        public static IConsumer<Zad1.Publ> Factory(string name, int divisibleBy, ConsoleColor color)
            => new Consumer(name, divisibleBy, color);
    }

    class Program
    {
        static async Task Main(string[] args)
        {

            var bus = Bus.Factory.CreateUsingRabbitMq(sbc =>
            {
                sbc.Host(new Uri(""), h =>
                {
                    h.Username("");
                    h.Password("");
                });

                sbc.ReceiveEndpoint("queue-A", ep =>
                {
                    ep.UseMessageRetry(r => r.Interval(5, TimeSpan.FromSeconds(0.1)));
                    ep.Consumer(() => Consumer.Factory("ReceiverA", 2, ConsoleColor.Green));
                });

                sbc.ReceiveEndpoint("queue-B", ep =>
                {
                    // To show that Publisher receives the error
                    ep.UseMessageRetry(r => { r.Immediate(1); });
                    ep.Consumer(() => Consumer.Factory("ReceiverB", 3, ConsoleColor.Cyan));
                });
            });

            await bus.StartAsync();

            Console.WriteLine($"[Receivers] Started");

            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;

            while (true) { }
        }
    }
}
