using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Zad7
{
    class Program
    {
        static int consumer1Count = 0;
        static int consumer2Count = 0;

        static object lockObj = new object();

        static string queue1Name = "abc_star_queue";
        static string queue2Name = "star_xyz_queue";

        static async Task Main(string[] args)
        {
            var factory = new ConnectionFactory()
            {
                UserName = "_",
                Password = "_",
                HostName = "_",
                VirtualHost = "_"
            };

            string exchangeName = "topic_exchange";
            string routingKey1 = "abc.def";
            string routingKey2 = "abc.xyz";

            try
            {
                using (var connection = await factory.CreateConnectionAsync())
                using (var channel = await connection.CreateChannelAsync())
                {
                    await channel.ExchangeDeclareAsync(exchangeName, "topic", durable: false, autoDelete: false);

                    await channel.QueueDeclareAsync(queue1Name, durable: false, exclusive: false, autoDelete: false);
                    await channel.QueueDeclareAsync(queue2Name, durable: false, exclusive: false, autoDelete: false);

                    await channel.QueueBindAsync(queue1Name, exchangeName, "abc.*");
                    await channel.QueueBindAsync(queue2Name, exchangeName, "*.xyz");
                }

                Console.WriteLine("[Publisher] Trying to connect with RabbitMQ...");
                using (var connection = await factory.CreateConnectionAsync())
                using (var channel = await connection.CreateChannelAsync())
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine("[Publisher] Connected with RabbitMQ. Sending 10 messages alternately to abc.def and abc.xyz...");
                    Console.ResetColor();

                    for (int i = 1; i <= 20; i++)
                    {
                        string currentRoutingKey = i % 2 == 0 ? routingKey2 : routingKey1;

                        string message = $"Message #{i} - Channel: {currentRoutingKey} - Time: {DateTime.Now.ToString("HH:mm:ss.fff")}";
                        var body = Encoding.UTF8.GetBytes(message);

                        var props = new BasicProperties();
                        props.Headers = new Dictionary<string, object>
                        {
                            { "job_time", i % 5 + 1 },
                            { "message_id", i }
                        };

                        await channel.BasicPublishAsync(exchangeName, currentRoutingKey, false, props, body);
                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.WriteLine($"[Publisher] Sent to {currentRoutingKey}: {message}");
                        Console.ResetColor();

                        Thread.Sleep(200);
                    }
                }

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("[Publisher] Sending ended");
                Console.ResetColor();

                Thread.Sleep(1000);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\nPress any key to start consumers ...");
            Console.ResetColor();
            Console.ReadKey();

            _ = Task.Run(() => StartConsumer(factory, exchangeName, "abc.*", queue1Name, 1, ConsoleColor.Green));

            _ = Task.Run(() => StartConsumer(factory, exchangeName, "*.xyz", queue2Name, 2, ConsoleColor.Cyan));


            Console.WriteLine("Press any key for results...");
            Console.ReadKey();

            printResults();
        }

        private static void printResults()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n===== RESULTS =====");
            Console.WriteLine($"Consumer 1 (abc.*) processed: {consumer1Count} messages");
            Console.WriteLine($"Consumer 2 (*.xyz) processed: {consumer2Count} messages");
            Console.WriteLine($"Total processed: {consumer1Count + consumer2Count} messages");
            Console.WriteLine("=======================\n");
            Console.ResetColor();
        }

        static async void StartConsumer(ConnectionFactory factory, string exchangeName, string bindingKey, string queueName, int id, ConsoleColor consolColor)
        {
            try
            {
                Console.ForegroundColor = consolColor;
                Console.WriteLine($"[Receiver {id}] Starting message receiver...");
                Console.ResetColor();

                using (var connection = await factory.CreateConnectionAsync())
                using (var channel = await connection.CreateChannelAsync())
                {
                    await channel.ExchangeDeclareAsync(exchangeName, "topic", durable: false, autoDelete: false);

                    await channel.QueueDeclareAsync(queueName, durable: false, exclusive: false, autoDelete: false);
                    await channel.QueueBindAsync(queueName, exchangeName, bindingKey);
                    await channel.BasicQosAsync(0, 1, false);

                    Console.ForegroundColor = consolColor;
                    Console.WriteLine($"[Receiver {id}] Waiting for messages with binding key: {bindingKey}");
                    Console.ResetColor();

                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.ReceivedAsync += async (model, ea) =>
                    {
                        try
                        {
                            var body = ea.Body.ToArray();
                            var message = Encoding.UTF8.GetString(body);
                            string routingKey = ea.RoutingKey;

                            int jobTime = 1;
                            int messageId = 0;

                            if (ea.BasicProperties.Headers != null)
                            {
                                if (ea.BasicProperties.Headers.ContainsKey("job_time"))
                                    jobTime = (int)ea.BasicProperties.Headers["job_time"];

                                if (ea.BasicProperties.Headers.ContainsKey("message_id"))
                                    messageId = (int)ea.BasicProperties.Headers["message_id"];
                            }

                            Console.ForegroundColor = consolColor;
                            Console.WriteLine($"[Receiver {id} {bindingKey}] Received from {routingKey}: {message} | Working {jobTime}s");
                            Console.ResetColor();
                            Thread.Sleep(jobTime * 200);

                            Console.ForegroundColor = consolColor;
                            Console.WriteLine($"[Receiver {id} {bindingKey}] Processed message from {routingKey}, waiting 2 seconds before acknowledging...");
                            Console.ResetColor();

                            await Task.Delay(2000);

                            lock (lockObj)
                            {
                                if (id == 1)
                                    consumer1Count++;
                                else if (id == 2)
                                    consumer2Count++;
                            }

                            await channel.BasicAckAsync(ea.DeliveryTag, false);
                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[Receiver {id} {bindingKey}] Processing error: {ex.Message}");
                            Console.ResetColor();
                            await channel.BasicNackAsync(ea.DeliveryTag, false, requeue: true);
                        }
                    };

                    await channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer);

                    Thread.Sleep(Timeout.Infinite);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[Receiver {id} {bindingKey}] Error: {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}