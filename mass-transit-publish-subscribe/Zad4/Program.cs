using MassTransit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Zad4
{
    public interface IMessage
    {
        string oldText { get; set; }
    }

    public interface IMessage2
    {
        string newText { get; set; }
    }

    public class Message : IMessage, IMessage2
    {
        public string oldText { get; set; }
        public string newText { get; set; }
    }

    public class MessageConsumerC : IConsumer<IMessage2>
    {
        public Task Consume(ConsumeContext<IMessage2> context)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"[ConsumerC] Received new text: {context.Message.newText} at {DateTime.Now}");
            Console.ForegroundColor = originalColor;
            return Task.CompletedTask;
        }
    }

    public class MessageConsumerB : IConsumer<IMessage>
    {
        private int messageCounter = 0;

        public Task Consume(ConsumeContext<IMessage> context)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[ConsumerB][Message {++messageCounter}] Received old text: {context.Message.oldText} at {DateTime.Now}");
            Console.ForegroundColor = originalColor;
            return Task.CompletedTask;
        }
    }

    public class MessageConsumerA : IConsumer<IMessage>
    {
        public Task Consume(ConsumeContext<IMessage> context)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[ConsumerA] Received message: {context.Message.oldText} at {DateTime.Now}");
            Console.ForegroundColor = originalColor;
            return Task.CompletedTask;
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var consumer = new MessageConsumerB();
            var bus = Bus.Factory.CreateUsingRabbitMq(sbc =>
            {
                sbc.Host(new Uri(""), h =>
                {
                    h.Username("");
                    h.Password("");
                });

                sbc.ReceiveEndpoint("message-queue-A", ep =>
                {
                    ep.Consumer<MessageConsumerA>();
                });

                sbc.ReceiveEndpoint("message-queue-B", ep =>
                {
                    ep.Instance(consumer);
                });

                sbc.ReceiveEndpoint("message-queue-C", ep =>
                {
                    ep.Consumer<MessageConsumerC>();
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
                    oldText = "Update your application to newer version",
                    newText = $"Dynamic message content #{i} - {Guid.NewGuid()}"

                };

                await bus.Publish(message);
                Console.WriteLine($"[Sender] Sent message #{i}: {message.oldText} and {message.newText}");

                await Task.Delay(500);
            }

            Console.ForegroundColor = originalColor;

            Console.WriteLine("\nAll messages sent. Press any key to exit");
            Console.ReadKey();

            await bus.StopAsync();
        }
    }
}