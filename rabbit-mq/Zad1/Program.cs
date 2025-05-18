using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Zad1
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var factory = new ConnectionFactory()
            {
                UserName = "_",
                Password = "_",
                HostName = "_",
                VirtualHost = "_"
            };

            string queueName = "message_queue";

            try
            {
                Console.WriteLine("[Sender] Trying to connect with RabbitMQ...");
                using (var connection = await factory.CreateConnectionAsync())
                using (var channel = await connection.CreateChannelAsync())
                {
                    await channel.QueueDeclareAsync(queueName, false, false, false, null);

                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine("[Sender] Connected with RabbitMQ. Sending 10 messages...");
                    Console.ResetColor();

                    for (int i = 1; i <= 10; i++)
                    {
                        string message = $"Message #{i} - Description: {DateTime.Now.ToString("HH:mm:ss.fff")}";
                        var body = Encoding.UTF8.GetBytes(message);
                        var props = new BasicProperties();
                        props.Headers = new Dictionary<string, object>
                        {
                            { "KSR1", i },
                            { "KSR2", i+10 }
                        };

                        await channel.BasicPublishAsync("", queueName, false, props, body);
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.WriteLine($"[Sender] Sent: {message}");
                        Console.ResetColor();

                        Thread.Sleep(200);
                    }
                }

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("[Sender] Sending ended");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.ReadKey();

            var consumerThread = new Thread(() => StartConsumer(factory, queueName));
            consumerThread.Start();
            Console.ReadKey();

        }

        static async void StartConsumer(ConnectionFactory factory, string queueName)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[Receiver] Starting message receiver...");
                Console.ResetColor();

                using (var connection = await factory.CreateConnectionAsync())
                using (var channel = await connection.CreateChannelAsync())
                {
                    await channel.QueueDeclareAsync(queueName, false, false, false, null);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[Receiver] Waiting for messages...");
                    Console.ResetColor();

                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.ReceivedAsync += async (model, ea) =>
                    {
                        var body = ea.Body.ToArray();
                        var message = Encoding.UTF8.GetString(body);

                        Console.ForegroundColor = ConsoleColor.Green;
                        int ksr1 = (int)ea.BasicProperties.Headers["KSR1"];
                        int ksr2 = (int)ea.BasicProperties.Headers["KSR2"];

                        Console.WriteLine($"[Receiver] Received: {message} | KSR1 : {ksr1} | KSR2: {ksr2}");
                        Console.ResetColor();
                        await Task.CompletedTask;
                    };

                    await channel.BasicConsumeAsync(queue: queueName, autoAck: true, consumer: consumer);

                    Thread.Sleep(Timeout.Infinite);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[Receiver] Error: {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}
