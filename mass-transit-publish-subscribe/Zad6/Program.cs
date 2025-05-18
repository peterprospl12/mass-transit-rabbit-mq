using MassTransit;
using System;

using System.Threading.Tasks;

namespace Zad6
{
    public interface IMessage1
    {
        string Text1 { get; set; }
    }

    public interface IMessage2
    {
        string Text2 { get; set; }
    }

    public interface IMessage3 : IMessage1, IMessage2
    {
        string Text3 { get; set; }
    }

    public class Message1 : IMessage1
    {
        public string Text1 { get; set; }
    }

    public class Message2 : IMessage2
    {
        public string Text2 { get; set; }
    }

    public class Message3 : IMessage3
    {
        public string Text1 { get; set; }
        public string Text2 { get; set; }
        public string Text3 { get; set; }
    }

    public class MessageConsumerC : IConsumer<IMessage2>
    {
        private int messageCounter = 0;

        public Task Consume(ConsumeContext<IMessage2> context)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.White;

            Console.WriteLine($"[ConsumerC][Message {++messageCounter}] Received message Type 2: {context.Message.Text2} at {DateTime.Now}");

            Console.ForegroundColor = originalColor;
            return Task.CompletedTask;
        }
    }

    public class MessageConsumerB : IConsumer<IMessage3>
    {
        private int messageCounter = 0;

        public Task Consume(ConsumeContext<IMessage3> context)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Magenta;

            Console.WriteLine($"[ConsumerB][Message {++messageCounter}] Received message Type 3: {context.Message.Text1} | {context.Message.Text2} | {context.Message.Text3} at {DateTime.Now}");

            Console.ForegroundColor = originalColor;
            return Task.CompletedTask;
        }
    }

    public class MessageConsumerA : IConsumer<IMessage1>
    {
        private int messageCounter = 0;

        public Task Consume(ConsumeContext<IMessage1> context)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;

            Console.WriteLine($"[ConsumerA][Message {++messageCounter}] Received message Type 1: {context.Message.Text1} at {DateTime.Now}");

            Console.ForegroundColor = originalColor;
            return Task.CompletedTask;
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var consumerA = new MessageConsumerA();
            var consumerB = new MessageConsumerB();
            var consumerC = new MessageConsumerC();

            var bus = Bus.Factory.CreateUsingRabbitMq(sbc =>
            {
                sbc.Host(new Uri(""), h =>
                {
                    h.Username("");
                    h.Password("");
                });

                sbc.ReceiveEndpoint("message-queue-A", ep =>
                {
                    ep.Instance(consumerA);
                });

                sbc.ReceiveEndpoint("message-queue-B", ep =>
                {
                    ep.Instance(consumerB);
                });

                sbc.ReceiveEndpoint("message-queue-C", ep =>
                {
                    ep.Instance(consumerC);
                });
            });

            await bus.StartAsync();

            Console.WriteLine("Bus started. Press key");
            Console.ReadKey();

            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;

            for (int i = 1; i <= 5; i++)
            {
                var message3 = new Message3
                {
                    Text1 = $"Message Type 1 #{i}",
                    Text2 = $"Message Type 2 #{i}",
                    Text3 = $"Message Type 3 #{i}"
                };

                Console.WriteLine($"[Sender] Sending message Type 1 #{i}: {message3.Text1}");
                Console.WriteLine($"[Sender] Sending message Type 2 #{i}: {message3.Text2}");
                Console.WriteLine($"[Sender] Sending message Type 3 #{i}: {message3.Text3}");

                await bus.Publish<IMessage3>(message3);

                await Task.Delay(200);
            }

            Console.ForegroundColor = originalColor;

            Console.WriteLine("\nAll messages sent. Press any key to exit");
            Console.ReadKey();

            await bus.StopAsync();
        }
    }
}