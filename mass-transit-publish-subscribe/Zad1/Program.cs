using MassTransit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Zad1
{
    public class Message
    {
        public string Content { get; set; }
        public int MessageNumber { get; set; }
    }

    class Program
    {
        public static Task Consume(ConsumeContext<Message> context)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;

            Console.WriteLine($"[Consumer] Received message #{context.Message.MessageNumber}: {context.Message.Content} at {DateTime.Now}");

            foreach (var hdr in context.Headers.GetAll())
            {
                Console.WriteLine("Key -> {0} \n Value -> {1}", hdr.Key, hdr.Value);
            }

            Console.ForegroundColor = originalColor;
            return Task.CompletedTask;
        }

        static async Task Main(string[] args)
        {
            var bus = Bus.Factory.CreateUsingRabbitMq(sbc =>
            {
                sbc.Host(new Uri(""), h =>
                {
                    h.Username("");
                    h.Password("");
                });

                sbc.ReceiveEndpoint("message-queue", ep =>
                {
                    ep.Handler<Message>(Consume);
                });
            });

            await bus.StartAsync();

            Console.WriteLine("Bus started. Press key");
            Console.ReadKey();

            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;

            for (int i = 1; i <= 10; i++)
            {
                var message = new Message
                {
                    MessageNumber = i,
                    Content = $"Dynamic message content #{i} - {Guid.NewGuid()}"
                };

                await bus.Publish(message, ctx =>
                {
                    ctx.Headers.Set("ksr1", i);
                    ctx.Headers.Set("ksr10", i + 10);
                });
                Console.WriteLine($"[Sender] Sent message #{i}: {message.Content}");

                await Task.Delay(200);
            }

            Console.ForegroundColor = originalColor;

            Console.WriteLine("\nAll messages sent. Press any key to exit");
            Console.ReadKey();

            await bus.StopAsync();
        }
    }
}