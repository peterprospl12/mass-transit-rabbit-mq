using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Zad5
{
    class Program
    {
        static int consumer1Count = 0;
        static int consumer2Count = 0;

        static object lockObj = new object();
        static string responseQueueName = "response_queue";

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
            _ = StartResponseListener(factory);

            try
            {
                Console.WriteLine("[Sender] Trying to connect with RabbitMQ...");
                using (var connection = await factory.CreateConnectionAsync())
                using (var channel = await connection.CreateChannelAsync())
                {
                    await channel.QueueDeclareAsync(queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);
                    await channel.QueueDeclareAsync(responseQueueName, durable: false, exclusive: false, autoDelete: false, arguments: null);

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
                            { "job_time", i % 5 + 1 },
                            { "message_id", i }
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

                Thread.Sleep(1000);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\nPress any key to start the first consumer...");
            Console.ResetColor();
            Console.ReadKey();

            _ = Task.Run(() => StartConsumer(factory, queueName, 1, ConsoleColor.Green, false));



            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\nPress any key to start the second consumer...");
            Console.ResetColor();
            Console.ReadKey();

            _ = Task.Run(() => StartConsumer(factory, queueName, 2, ConsoleColor.Cyan, true));

            Thread.Sleep(15000);

            printResults();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static void printResults()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n===== RESULTS =====");
            Console.WriteLine($"Consumer 1 processed: {consumer1Count} messages");
            Console.WriteLine($"Consumer 2 processed: {consumer2Count} messages");
            Console.WriteLine($"Total processed: {consumer1Count + consumer2Count} messages");
            Console.WriteLine("=======================\n");
            Console.ResetColor();
        }

        static async Task StartResponseListener(ConnectionFactory factory)
        {
            try
            {
                using (var connection = await factory.CreateConnectionAsync())
                using (var channel = await connection.CreateChannelAsync())
                {
                    await channel.QueueDeclareAsync(responseQueueName, false, false, false, null);

                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.ReceivedAsync += async (model, ea) =>
                    {
                        try
                        {
                            var body = ea.Body.ToArray();
                            var responseMessage = Encoding.UTF8.GetString(body);
                            int messageId = -1;

                            if (ea.BasicProperties.Headers.ContainsKey("original_message_id"))
                            {
                                messageId = (int)ea.BasicProperties.Headers["original_message_id"];
                            }

                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.WriteLine($"[Sender] Received response for message #{messageId}: {responseMessage}");
                            Console.ResetColor();

                            await channel.BasicAckAsync(ea.DeliveryTag, false);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing response: {ex.Message}");
                            await channel.BasicNackAsync(ea.DeliveryTag, false, true);
                        }
                    };

                    await channel.BasicConsumeAsync(queue: responseQueueName, autoAck: false, consumer: consumer);

                    await Task.Delay(Timeout.Infinite);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Response listener error: {ex.Message}");
                Console.ResetColor();
            }
        }


        static async void StartConsumer(ConnectionFactory factory, string queueName, int id, ConsoleColor consolColor, bool shouldResponse)
        {
            try
            {
                Console.ForegroundColor = consolColor;
                Console.WriteLine($"[Receiver {id}] Starting message receiver...");
                Console.ResetColor();

                using (var connection = await factory.CreateConnectionAsync())
                using (var channel = await connection.CreateChannelAsync())
                {
                    await channel.QueueDeclareAsync(queueName, false, false, false, null);
                    await channel.BasicQosAsync(0, 1, false);

                    if (shouldResponse)
                    {
                        await channel.QueueDeclareAsync(responseQueueName, false, false, false, null);
                    }

                    Console.ForegroundColor = consolColor;
                    Console.WriteLine("[Receiver] Waiting for messages...");
                    Console.ResetColor();

                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.ReceivedAsync += async (model, ea) =>
                    {
                        try
                        {
                            var body = ea.Body.ToArray();
                            var message = Encoding.UTF8.GetString(body);

                            int jobTime = (int)ea.BasicProperties.Headers["job_time"];
                            int messageId = (int)ea.BasicProperties.Headers["message_id"];

                            Console.ForegroundColor = consolColor;
                            Console.WriteLine($"[Receiver {id}] Received: {message} | Working {jobTime}s");
                            Console.ResetColor();
                            Thread.Sleep(jobTime * 200);

                            Console.ForegroundColor = consolColor;
                            Console.WriteLine($"[Receiver {id}] Processed message, waiting 2 seconds before acknowledging...");
                            Console.ResetColor();

                            await Task.Delay(2000);

                            lock (lockObj)
                            {
                                if (id == 1)
                                    consumer1Count++;
                                else if (id == 2)
                                    consumer2Count++;
                            }

                            if (shouldResponse)
                            {
                                string responseMessage = $"Response from consumer {id} for message #{messageId}: Processing completed at {DateTime.Now.ToString("HH:mm:ss.fff")}";
                                var responseBody = Encoding.UTF8.GetBytes(responseMessage);

                                var props = new BasicProperties();
                                props.Headers = new Dictionary<string, object>
                                {
                                    { "original_message_id", messageId }
                                };

                                Console.ForegroundColor = consolColor;
                                Console.WriteLine($"[Receiver {id}] Sending response for message #{messageId}");
                                Console.ResetColor();

                                await channel.BasicPublishAsync("", responseQueueName, false, props, responseBody);
                            }

                            await channel.BasicAckAsync(ea.DeliveryTag, false);
                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"[Receiver {id}] Processing error: {ex.Message}");
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
                Console.WriteLine($"[Receiver {id}] Error: {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}
