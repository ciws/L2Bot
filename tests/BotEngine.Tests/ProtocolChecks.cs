using System.Diagnostics;
using Client.Domain.DTO;
using Client.Domain.Entities;
using Client.Domain.Enums;
using Client.Domain.Service;
using Client.Infrastructure.Factories;
using Client.Infrastructure.Parsers;
using Client.Infrastructure.Transports;
using Newtonsoft.Json.Linq;

static class ProtocolChecks
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    public static async Task Run(string[] args)
    {
        var factory = new ItemFactory();
        foreach (var (type, expected) in new[] { (0, typeof(EtcItem)), (1, typeof(ArmorItem)), (2, typeof(WeaponItem)), (3, typeof(ShieldItem)) })
        {
            var item = factory.Create($$"""{"id":42,"type":"{{type}}","name":"Test","amount":12,"pAttack":500,"pDefense":250} """)!;
            Check(item.GetType()==expected, "Correct concrete inventory type");
            var changed = 0; item.PropertyChanged += (_, _) => changed++;
            factory.Update(item,"{\"id\":42,\"name\":\"Updated\",\"amount\":3,\"isAutoused\":\"1\"}");
            Check(item.Name=="Updated" && item.Amount==3 && item.IsAutoused && changed==1,"Inventory update notifies bindings");
        }
        var effects = new CountingHandler<AbnormalEffect>(new EntityFactory<AbnormalEffect>(), new EntityFactory<Entity>());
        effects.Update(MessageOperationEnum.Create,"{\"id\":1204,\"level\":2,\"name\":\"Wind Walk\"}");
        var original=effects.GetEntity(1204);
        effects.Update(MessageOperationEnum.Create,"{\"id\":1204,\"level\":3,\"name\":\"Wind Walk\"}");
        Check(effects.Created==1 && effects.GetEntity(1204)==original && original!.Level==3,"Snapshot replay upserts existing entities");
        effects.Reset(); Check(effects.Deleted==1 && effects.GetEntity(1204)==null,"Reconnect removes stale state");
        Console.WriteLine("PASS typed inventory, notifications, effect snapshot replay and reset");

        var builder = new JsonOutgoingMessageBuilder();
        var commands = new List<string>();
        foreach (var type in Enum.GetValues<OutgoingMessageTypeEnum>())
        {
            commands.Add(type==OutgoingMessageTypeEnum.Move
                ? builder.Build(new OutgoingMessage<Client.Domain.ValueObjects.Vector3>(type,new(10,-20,30)))
                : builder.Build(new OutgoingMessage<uint>(type,42)));
        }
        Check(JObject.Parse(commands[1])["Content"] is JObject,"Move content must be a JSON object");
        Check(JObject.Parse(commands[2])["Content"]!.Type==JTokenType.Integer,"Target content must be a JSON number");
        var native = args.FirstOrDefault();
        if (native==null) { Console.WriteLine("PASS JSON command shapes (pass native test executable for cross-language dispatch)"); return; }
        var fixture=Path.GetTempFileName();
        try
        {
            await File.WriteAllLinesAsync(fixture,commands);
            using var child=Process.Start(new ProcessStartInfo(native) { ArgumentList={ "--commands",fixture }, UseShellExecute=false })!;
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try { await child.WaitForExitAsync(timeout.Token); }
            catch { child.Kill(true); throw; }
            Check(child.ExitCode==0,"Native parser must accept real client commands");
        }
        finally { File.Delete(fixture); }
        if (OperatingSystem.IsWindows()) await TestPipe(native,builder);
    }
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    static async Task TestPipe(string native, JsonOutgoingMessageBuilder builder)
    {
        var name="L2Bot-test-"+Guid.NewGuid().ToString("N");
        using var child=Process.Start(new ProcessStartInfo(native) { ArgumentList={ "--pipe-server",name }, UseShellExecute=false })!;
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var transport=new NamedPipeTransport(name);
        try
        {
            for(var session=0;session<2;session++)
            {
                using var receiveCancellation=CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
                var received=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                void Receive(string message)
                {
                    var parsed=new JsonMessageParser().Parse(message);
                    if(parsed.Type==MessageTypeEnum.Item && parsed.Operation==MessageOperationEnum.Create) received.TrySetResult(true);
                }
                transport.Message+=Receive;
                await transport.ConnectAsync(timeout.Token);
                await transport.SendAsync(builder.Build(new EmptyOutgoingMessage(OutgoingMessageTypeEnum.Invalidate)),timeout.Token);
                var reading=transport.StartReceiveAsync(receiveCancellation.Token);
                await received.Task.WaitAsync(timeout.Token);
                // Require an additional full snapshot after a second Invalidate in the same session.
                received=new(TaskCreationOptions.RunContinuationsAsynchronously);
                await transport.SendAsync(builder.Build(new EmptyOutgoingMessage(OutgoingMessageTypeEnum.Invalidate)),timeout.Token);
                await received.Task.WaitAsync(timeout.Token);
                receiveCancellation.Cancel();
                try { await reading; } catch(OperationCanceledException) { }
                transport.Message-=Receive;
            }
            await transport.ConnectAsync(timeout.Token);
            await transport.SendAsync(builder.Build(new EmptyOutgoingMessage(OutgoingMessageTypeEnum.Stand)),timeout.Token);
            await child.WaitForExitAsync(timeout.Token);
            Check(child.ExitCode==0,"Native world and pipe must stop cleanly");
            Console.WriteLine("PASS real Windows named pipe handshake, full resync, reconnect, and shutdown");
        }
        finally { transport.Dispose(); if(!child.HasExited) child.Kill(true); }
    }
    sealed class CountingHandler<T> : EntityHandler<T> where T:class,Client.Domain.Entities.EntityInterface
    {
        public int Created,Deleted;
        public CountingHandler(Client.Domain.Factories.EntityFactoryInterface<T> factory,Client.Domain.Factories.EntityFactoryInterface<Entity> entities) : base(factory,entities) { }
        public override void OnCreate(T entity) => Created++;
        public override void OnDelete(T entity) => Deleted++;
    }
}
