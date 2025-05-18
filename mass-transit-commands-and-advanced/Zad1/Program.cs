using Controller;
using MassTransit;
using MassTransit.Serialization;

using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Zad1;

namespace Publisher
{

    public class Observer : IPublishObserver, IConsumeObserver
    {
        class Counters
        {
            public int attempted;
            public int consumed;
            public int published;
        }

        readonly ConcurrentDictionary<Type, Counters> _stats = new ConcurrentDictionary<Type, Counters>();

        Counters GetCounters(Type messageType) =>
            _stats.GetOrAdd(messageType, _ => new Counters());

        public Task PrePublish<T>(PublishContext<T> context) where T : class
        {
            return Task.CompletedTask;
        }

        public Task PostPublish<T>(PublishContext<T> context) where T : class
        {
            var actualType = context.Message.GetType();
            Interlocked.Increment(ref GetCounters(actualType).published);
            return Task.CompletedTask;
        }

        public Task PublishFault<T>(PublishContext<T> context, Exception exception) where T : class
        {
            return Task.CompletedTask;
        }

        public Task PreConsume<T>(ConsumeContext<T> context) where T : class
        {
            var actualType = context.Message.GetType();
            Interlocked.Increment(ref GetCounters(actualType).attempted);
            return Task.CompletedTask;
        }

        public Task PostConsume<T>(ConsumeContext<T> context) where T : class
        {
            var actualType = context.Message.GetType();
            Interlocked.Increment(ref GetCounters(actualType).consumed);
            return Task.CompletedTask;
        }

        public Task ConsumeFault<T>(ConsumeContext<T> context, Exception exception) where T : class
        {
            return Task.CompletedTask;
        }

        public void DumpStats()
        {
            Console.WriteLine();
            Console.WriteLine("=== STATS ===");
            foreach (var stat in _stats)
            {
                var type = stat.Key.Name;
                var value = stat.Value;
                Console.WriteLine($"[{type}] Attempts: {value.attempted}, Consumed: {value.consumed}, Published: {value.published}");
            }
            Console.WriteLine();
        }
    }

    class Program
    {
        public static readonly object _consoleLock = new object();


        public static Task handleFault(ConsumeContext<Fault<Zad1.Publ>> ctx)
        {
            lock (_consoleLock)
            {
                Console.WriteLine();
                Console.WriteLine($"[Publisher] Fault received for message ID: {ctx.Message.Message.Number}");

                foreach (var e in ctx.Message.Exceptions)
                {
                    Console.WriteLine($" - Exception type: {e.ExceptionType}");
                    Console.WriteLine($" - Message: {e.Message}");
                    Console.WriteLine($" - Source: {e.Source}");

                    if (ctx.Message.Host != null)
                    {
                        Console.WriteLine($" - Failed on host: {ctx.Message.Host.MachineName}");
                        Console.WriteLine($" - Process: {ctx.Message.Host.ProcessName} (ID: {ctx.Message.Host.ProcessId})");
                    }
                }
            }
            return Task.CompletedTask;
        }

        public static Task handleResponse(ConsumeContext<Zad1.Response> ctx)
        {
            Console.WriteLine();
            Console.WriteLine($"[Publisher] Received response = {ctx.Message.message}");
            Console.WriteLine();
            return Task.CompletedTask;
        }

        static async Task Main(string[] args)
        {
            bool send = false;
            var observer = new Observer();

            var bus = Bus.Factory.CreateUsingRabbitMq(sbc =>
                {
                    sbc.Host(new Uri(""), h =>
                    {
                        h.Username("");
                        h.Password("");
                    });

                    sbc.ReceiveEndpoint("publisher", ep =>
                    {

                        ep.UseEncryptedSerializer(new AesCryptoStreamProvider(new Provider("42324142324142324142324142324119"), "4232414232414235"));

                        ep.Handler<ControlCommand>(context =>
                        {
                            send = context.Message.send;
                            Console.WriteLine();
                            Console.WriteLine($"[Publisher] Received command send = {context.Message.send}");
                            Console.WriteLine();
                            return Task.CompletedTask;
                        });
                    });

                    sbc.ReceiveEndpoint("publisher-responses", ep =>
                    {
                        ep.Handler<Zad1.ResponseA>(context => handleResponse(context));

                        ep.Handler<Zad1.ResponseB>(context => handleResponse(context));
                    });

                    sbc.ReceiveEndpoint("faults", ep =>
                    {
                        ep.Handler<Fault<Zad1.Publ>>(context => handleFault(context));
                    });

                    sbc.ConnectPublishObserver(observer);
                    sbc.ConnectConsumeObserver(observer);
                });

            await bus.StartAsync();

            Console.WriteLine($"[Publisher] Started | [s] = dump observer, [b] = exit");

            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;

            int i = 1;

            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey();
                    if (key.Key == ConsoleKey.S)
                    {
                        observer.DumpStats();
                    }

                    if (key.Key == ConsoleKey.B)
                    {
                        break;
                    }
                }

                if (!send) continue;

                await bus.Publish(new Publ { Number = i });
                Console.WriteLine($"[Publisher] Publ #{i}");
                i++;
                await Task.Delay(1000);
            }

            Console.WriteLine("[Publisher] Stopped.");
            Console.ForegroundColor = originalColor;
            await bus.StopAsync();
        }
    }
}