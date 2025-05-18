using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Zad4
{
    class Program
    {
        static int consumer1Count = 0;
        static int consumer2Count = 0;
        static int consumer3Count = 0;
        static int consumer4Count = 0;

        static object lockObj = new object();

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

            var consumerThread = new Thread(() => StartConsumer(factory, queueName, 1));
            var consumerThread1 = new Thread(() => StartConsumer(factory, queueName, 2));
            var consumerThread2 = new Thread(() => StartConsumer(factory, queueName, 3));
            var consumerThread3 = new Thread(() => StartConsumer(factory, queueName, 4));

            //consumerThread2.Start();
            //consumerThread3.Start();


            try
            {
                Console.WriteLine("[Sender] Trying to connect with RabbitMQ...");
                using (var connection = await factory.CreateConnectionAsync())
                using (var channel = await connection.CreateChannelAsync())
                {
                    await channel.QueueDeclareAsync(queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);

                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine("[Sender] Connected with RabbitMQ. Sending 10 messages...");
                    Console.ResetColor();

                    for (int i = 1; i <= 30; i++)
                    {
                        string message = $"Message #{i} - Description: {DateTime.Now.ToString("HH:mm:ss.fff")}";
                        var body = Encoding.UTF8.GetBytes(message);

                        var props = new BasicProperties();
                        props.Headers = new Dictionary<string, object>
                        {
                            { "job_time", i % 5 + 1 }
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
            consumerThread.Start();
            consumerThread1.Start();
            Thread.Sleep(12000);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n===== RESULTS =====");
            Console.WriteLine($"Consumer 1 processed: {consumer1Count} messages");
            Console.WriteLine($"Consumer 2 processed: {consumer2Count} messages");
            Console.WriteLine($"Consumer 3 processed: {consumer3Count} messages");
            Console.WriteLine($"Consumer 4 processed: {consumer4Count} messages");
            Console.WriteLine($"Total processed: {consumer1Count + consumer2Count + consumer3Count + consumer4Count} messages");
            Console.WriteLine("=======================\n");
            Console.ResetColor();
            Console.ReadKey();

        }

        static async void StartConsumer(ConnectionFactory factory, string queueName, int id)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[Receiver {id}] Starting message receiver...");
                Console.ResetColor();

                using (var connection = await factory.CreateConnectionAsync())
                using (var channel = await connection.CreateChannelAsync())
                {
                    await channel.QueueDeclareAsync(queueName, false, false, false, null);
                    await channel.BasicQosAsync(0, 1, false);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("[Receiver] Waiting for messages...");
                    Console.ResetColor();

                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.ReceivedAsync += async (model, ea) =>
                    {
                        try
                        {
                            var body = ea.Body.ToArray();
                            var message = Encoding.UTF8.GetString(body);

                            Console.ForegroundColor = ConsoleColor.Green;
                            int jobTime = (int)ea.BasicProperties.Headers["job_time"];

                            Console.WriteLine($"[Receiver {id}] Received: {message} | Working {jobTime}s");
                            Console.ResetColor();
                            Thread.Sleep(jobTime * 200);

                            lock (lockObj)
                            {
                                if (id == 1)
                                    consumer1Count++;
                                else if (id == 2)
                                    consumer2Count++;
                                else if (id == 3)
                                    consumer3Count++;
                                else if (id == 4)
                                    consumer4Count++;
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
