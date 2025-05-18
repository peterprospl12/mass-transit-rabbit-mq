using MassTransit;

namespace Shop
{
    public static class ConsoleHelper
    {
        private static readonly object _lock = new object();
        public static void WriteLocked(ConsoleColor color, string message)
        {

            lock (_lock)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(message);
                Console.ResetColor();
            }
        }
    }

    public class ClientConfirmationTimeout : CorrelatedBy<Guid>
    {
        public Guid CorrelationId { get; set; }
    }

    public class WarehouseConfirmationTimeout : CorrelatedBy<Guid>
    {
        public Guid CorrelationId { get; set; }
    }

    public class OrderData : SagaStateMachineInstance
    {
        public Guid CorrelationId { get; set; }
        public required string ClientLogin { get; set; }
        public Guid? clientTimeout { get; set; }
        public Guid? warehouseTimeout { get; set; }
        public string? CurrentStatus { get; set; }

        public int Quantity { get; set; }
        public bool ClientConfirmed { get; set; }
        public bool WarehouseConfirmed { get; set; }
    }

    public class ClientConfirmation : CorrelatedBy<Guid>
    {
        public Guid CorrelationId { get; set; }
    }

    public class ClientNoConfirmation : CorrelatedBy<Guid>
    {
        public Guid CorrelationId { get; set; }
    }

    public class AskForClientConfirmation
    {
        public Guid Id { get; set; }
        public int Quantity { get; set; }
        public required string ClientLogin { get; set; }
    }

    public class WarehouseConfirmation : CorrelatedBy<Guid>
    {
        public Guid CorrelationId { get; set; }
    }

    public class WarehouseNoConfirmation : CorrelatedBy<Guid>
    {
        public Guid CorrelationId { get; set; }
    }

    public class AskForWarehouseConfirmation
    {
        public Guid Id { get; set; }
        public int Quantity { get; set; }
    }

    public class OrderAccepted
    {
        public Guid Id { get; set; }
        public int Quantity { get; set; }
        public required string ClientLogin { get; set; }
    }

    public class OrderCanceled
    {
        public Guid Id { get; set; }
        public int Quantity { get; set; }
        public required string ClientLogin { get; set; }
    }

    public class OrderSaga : MassTransitStateMachine<OrderData>
    {
        public State? AwaitingConfirmation { get; private set; }
        public State? Confirmed { get; private set; }
        public State? Canceled { get; private set; }
        public Event<StartOrder>? StartOrder { get; private set; }
        public Event<ClientConfirmation>? ClientConfirmation { get; private set; }
        public Event<ClientNoConfirmation>? ClientNoConfirmation { get; private set; }
        public Event<WarehouseConfirmation>? WarehouseConfirmation { get; private set; }
        public Event<WarehouseNoConfirmation>? WarehouseNoConfirmation { get; private set; }

        public Schedule<OrderData, ClientConfirmationTimeout>? ClientTimeoutSchedule { get; private set; }
        public Schedule<OrderData, WarehouseConfirmationTimeout>? WarehouseTimeoutSchedule { get; private set; }

        public OrderSaga()
        {
            InstanceState(x => x.CurrentStatus);
            Event(() => StartOrder, x => x
                .CorrelateBy((instance, context) => false)
                .SelectId(context => Guid.NewGuid())
            );

            Event(() => ClientConfirmation, e => e.CorrelateById(context => context.Message.CorrelationId));
            Event(() => ClientNoConfirmation, e => e.CorrelateById(context => context.Message.CorrelationId));
            Event(() => WarehouseConfirmation, e => e.CorrelateById(context => context.Message.CorrelationId));
            Event(() => WarehouseNoConfirmation, e => e.CorrelateById(context => context.Message.CorrelationId));

            Schedule(() => ClientTimeoutSchedule,
                x => x.clientTimeout,
                cfg =>
                {
                    cfg.Delay = TimeSpan.FromSeconds(10);
                    cfg.Received = r => r.CorrelateById(context => context.Message.CorrelationId);
                });
            Schedule(() => WarehouseTimeoutSchedule,
                x => x.warehouseTimeout,
                cfg =>
                {
                    cfg.Delay = TimeSpan.FromSeconds(10);
                    cfg.Received = r => r.CorrelateById(context => context.Message.CorrelationId);
                });

            Initially(
                When(StartOrder)
                    .Then(ctx =>
                    {
                        ctx.Saga.Quantity = ctx.Message.Quantity;
                        ctx.Saga.ClientConfirmed = false;
                        ctx.Saga.WarehouseConfirmed = false;
                        ctx.Saga.ClientLogin = ctx.Message.ClientLogin;
                        ConsoleHelper.WriteLocked(ConsoleColor.Cyan,
                            $"Order {ctx.Saga.CorrelationId} for {ctx.Saga.ClientLogin} started with quantity {ctx.Saga.Quantity}"
                        );
                    })
                    .TransitionTo(AwaitingConfirmation)
                    .ThenAsync(async ctx =>
                    {
                        await ctx.Publish(new AskForClientConfirmation
                        {
                            Id = ctx.Saga.CorrelationId,
                            Quantity = ctx.Saga.Quantity,
                            ClientLogin = ctx.Saga.ClientLogin
                        });

                        await ctx.Publish(new AskForWarehouseConfirmation
                        {
                            Id = ctx.Saga.CorrelationId,
                            Quantity = ctx.Saga.Quantity
                        });
                    })
                    .Schedule(ClientTimeoutSchedule, ctx => new ClientConfirmationTimeout { CorrelationId = ctx.Saga.CorrelationId })
                    .Schedule(WarehouseTimeoutSchedule, ctx => new WarehouseConfirmationTimeout { CorrelationId = ctx.Saga.CorrelationId })
            );
            During(AwaitingConfirmation,
                When(ClientConfirmation)
                    .Unschedule(ClientTimeoutSchedule)
                    .Then(ctx =>
                    {
                        ctx.Saga.ClientConfirmed = true;
                        ConsoleHelper.WriteLocked(ConsoleColor.Green,
                            $"Order {ctx.Saga.CorrelationId} confirmed by client"
                        );
                    })
                    .IfElse(ctx => ctx.Saga.ClientConfirmed && ctx.Saga.WarehouseConfirmed,
                    confirmActivity => confirmActivity
                        .Then(ctx =>
                        {
                            ConsoleHelper.WriteLocked(ConsoleColor.Green,
                                $"Order {ctx.Saga.CorrelationId} fully confirmed"
                            );
                        })
                        .ThenAsync(async ctx =>
                        {
                            await ctx.Publish(new OrderAccepted
                            {
                                Id = ctx.Saga.CorrelationId,
                                Quantity = ctx.Saga.Quantity,
                                ClientLogin = ctx.Saga.ClientLogin
                            });
                        })
                        .TransitionTo(Confirmed)
                        .Finalize(),
                        pendingActivity => pendingActivity
                            .Then(ctx =>
                            {
                                ConsoleHelper.WriteLocked(ConsoleColor.Yellow,
                                    $"Order {ctx.Saga.CorrelationId} pending confirmation from warehouse"
                                );
                            })
                    )
            );
            During(AwaitingConfirmation,
                When(WarehouseConfirmation)
                    .Unschedule(WarehouseTimeoutSchedule)
                    .Then(ctx =>
                    {
                        ctx.Saga.WarehouseConfirmed = true;
                        ConsoleHelper.WriteLocked(ConsoleColor.Green,
                            $"Order {ctx.Saga.CorrelationId} confirmed by warehouse"
                        );
                    })
                    .IfElse(ctx => ctx.Saga.ClientConfirmed && ctx.Saga.WarehouseConfirmed,
                    confirmActivity => confirmActivity
                        .Then(ctx =>
                        {
                            ConsoleHelper.WriteLocked(ConsoleColor.Green,
                                $"Order {ctx.Saga.CorrelationId} fully confirmed"
                            );
                        })
                        .ThenAsync(async ctx =>
                        {
                            await ctx.Publish(new OrderAccepted
                            {
                                Id = ctx.Saga.CorrelationId,
                                Quantity = ctx.Saga.Quantity,
                                ClientLogin = ctx.Saga.ClientLogin
                            });
                        })
                        .TransitionTo(Confirmed)
                        .Finalize(),
                        pendingActivity => pendingActivity
                            .Then(ctx =>
                            {
                                ConsoleHelper.WriteLocked(ConsoleColor.Yellow,
                                    $"Order {ctx.Saga.CorrelationId} pending confirmation from client"
                                );
                            })
                    )
            );
            During(AwaitingConfirmation,
                When(ClientNoConfirmation)
                    .Then(ctx =>
                    {
                        ConsoleHelper.WriteLocked(ConsoleColor.Red,
                            $"Order {ctx.Saga.CorrelationId} canceled by client"
                        );
                    })
                    .ThenAsync(async ctx =>
                    {
                        await ctx.Publish(new OrderCanceled
                        {
                            Id = ctx.Saga.CorrelationId,
                            Quantity = ctx.Saga.Quantity,
                            ClientLogin = ctx.Saga.ClientLogin
                        });
                    })
                    .TransitionTo(Canceled)
                    .Finalize()
            );
            During(AwaitingConfirmation,
                When(WarehouseNoConfirmation)
                    .Then(ctx =>
                    {
                        ConsoleHelper.WriteLocked(ConsoleColor.Red,
                            $"Order {ctx.Saga.CorrelationId} canceled by warehouse"
                        );
                    })
                    .ThenAsync(async ctx =>
                    {
                        await ctx.Publish(new OrderCanceled
                        {
                            Id = ctx.Saga.CorrelationId,
                            Quantity = ctx.Saga.Quantity,
                            ClientLogin = ctx.Saga.ClientLogin
                        });
                    })
                    .TransitionTo(Canceled)
                    .Finalize()
            );
            During(AwaitingConfirmation,
                When(ClientTimeoutSchedule!.Received)
                    .Then(ctx =>
                    {
                        ConsoleHelper.WriteLocked(ConsoleColor.Red,
                            $"Order {ctx.Saga.CorrelationId} timed out waiting for client confirmation"
                        );
                    })
                    .ThenAsync(async ctx =>
                    {
                        await ctx.Publish(new OrderCanceled
                        {
                            Id = ctx.Saga.CorrelationId,
                            Quantity = ctx.Saga.Quantity,
                            ClientLogin = ctx.Saga.ClientLogin
                        });
                    })
                    .TransitionTo(Canceled)
                    .Finalize()
            );

            During(AwaitingConfirmation,
                When(WarehouseTimeoutSchedule!.Received)
                    .Then(ctx =>
                    {
                        ConsoleHelper.WriteLocked(ConsoleColor.Red,
                            $"Order {ctx.Saga.CorrelationId} timed out waiting for warehouse confirmation"
                        );
                    })
                    .ThenAsync(async ctx =>
                    {
                        await ctx.Publish(new OrderCanceled
                        {
                            Id = ctx.Saga.CorrelationId,
                            Quantity = ctx.Saga.Quantity,
                            ClientLogin = ctx.Saga.ClientLogin
                        });
                    })
                    .TransitionTo(Canceled)
                    .Finalize()
            );
        }
    }

    public class StartOrder
    {
        public int Quantity { get; set; }
        public required string ClientLogin { get; set; }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            var repo = new InMemorySagaRepository<OrderData>();
            var machine = new OrderSaga();
            var bus = Bus.Factory.CreateUsingRabbitMq(sbc =>
                {
                    sbc.Host(new Uri(""), h =>
                    {
                        h.Username("");
                        h.Password("");
                    });

                    sbc.UseInMemoryScheduler();

                    sbc.ReceiveEndpoint("order-saga", e =>
                    {
                        e.StateMachineSaga(machine, repo);
                    });
                });

            await bus.StartAsync();
            Console.WriteLine("Shop started | Press any key to exit...");
            Console.ReadKey();
            await bus.StopAsync();
            Console.WriteLine("Shop stopped");
        }
    }
}
