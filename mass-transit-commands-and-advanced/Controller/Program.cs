using MassTransit;
using MassTransit.Serialization;
using System;
using System.Data;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Controller
{
    public class ControlCommand
    {
        public bool send { get; set; }
    }

    public class SymKey : SymmetricKey
    {
        public byte[] Key { get; set; }
        public byte[] IV { get; set; }
    }

    public class Provider : ISymmetricKeyProvider
    {
        private readonly string encryptionKey;

        public Provider(string secretKey)
        {
            encryptionKey = secretKey;
        }

        public bool TryGetKey(string keyId, out SymmetricKey symmetricKey)
        {
            var sk = new SymKey();

            sk.IV = Encoding.ASCII.GetBytes(keyId.Substring(0, 16));
            sk.Key = Encoding.ASCII.GetBytes(encryptionKey);

            symmetricKey = sk;
            return true;
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var cts = new CancellationTokenSource();

            var bus = Bus.Factory.CreateUsingRabbitMq(sbc =>
            {
                sbc.Host(new Uri(""), h =>
                {
                    h.Username("");
                    h.Password("");
                });

                sbc.UseEncryptedSerializer(new AesCryptoStreamProvider(new Provider("42324142324142324142324142324119"), "4232414232414235"));

            });

            await bus.StartAsync();

            Console.WriteLine("[Controller] Started | [s] = start, [t] = stop, [b] = exit");

            var tsk = bus.GetSendEndpoint(new Uri(".../publisher"));
            tsk.Wait();
            var sendEp = tsk.Result;

            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Blue;

            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.KeyChar == 's' || key.KeyChar == 't')
                {
                    var command = new ControlCommand { send = key.KeyChar == 's' };
                    await sendEp.Send(command, ctx =>
                    {
                        ctx.Headers.Set(EncryptedMessageSerializer.EncryptionKeyHeader, Guid.NewGuid().ToString());
                    });
                    Console.WriteLine($"[Controller] Sent: works = {command.send}");
                }
                else if (key.KeyChar == 'b')
                {
                    Console.WriteLine("[Controller] Exiting...");
                    break;
                }
            }


            Console.WriteLine("[Controller] Stopped.");
            Console.ForegroundColor = originalColor;
            await bus.StopAsync();
        }

    }
}
