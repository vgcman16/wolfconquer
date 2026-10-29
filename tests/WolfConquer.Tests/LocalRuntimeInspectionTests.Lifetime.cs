using System.Buffers.Binary;
using System.Text.Json;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LocalRuntimeInspectionTests
{
    [WindowsOperatorPipeFact]
    public Task RealMailWcm2AndInspectorWri1CoexistUnderTheSameBoundOwner() => WithPipe(async (f, inspector) =>
    {
        var previous = Environment.GetEnvironmentVariable("WOLFCONQUER_OPERATOR_MAIL");
        LocalOperatorMailPipe? mail = null;
        try
        {
            Environment.SetEnvironmentVariable("WOLFCONQUER_OPERATOR_MAIL", "1");
            mail = Assert.IsType<LocalOperatorMailPipe>(LocalOperatorMailPipe.StartIfEnabled(f.Handler, f.Runtime, f.Domain));
            Assert.NotEqual(mail.PipeName, inspector.PipeName);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var peer = Peer(mail.PipeName); await peer.ConnectAsync(deadline.Token);
            var request = f.Request(Guid.NewGuid());
            var body = JsonSerializer.SerializeToUtf8Bytes(request, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header, body.Length);
            await peer.WriteAsync(header, deadline.Token); await peer.WriteAsync(body, deadline.Token); await peer.FlushAsync(deadline.Token);
            await peer.ReadExactlyAsync(header, deadline.Token); var length = BinaryPrimitives.ReadInt32LittleEndian(header);
            Assert.InRange(length, 1, LocalOperatorMailPipe.MaximumResponseBytes);
            var response = new byte[length]; await peer.ReadExactlyAsync(response, deadline.Token);
            var delivered = LocalOperatorMailPipe.ParseResult(response);
            Assert.Equal(request.OperationId, delivered.OperationId);
            Assert.Equal(LegacyOperatorGearMailStatus.CommittedNotificationCompleted, delivered.Status);
            await peer.WriteAsync(LocalOperatorMailPipe.CreateResponseReceipt(delivered.OperationId), deadline.Token); await peer.FlushAsync(deadline.Token);
            var before = Files(f); var sent = f.SentSnapshot(); var water = f.HighWater;
            Diagnostic(await Exchange(inspector.PipeName, Query(f)));
            Assert.Equal(water, f.HighWater); Unchanged(f, before, sent);
        }
        finally
        {
            try { if (mail is not null) await mail.DisposeAsync(); }
            finally { Environment.SetEnvironmentVariable("WOLFCONQUER_OPERATOR_MAIL", previous); }
        }
    });

    [WindowsOperatorPipeFact]
    public Task ActualOwnedRuntimeShutdownCancelsInspectorAndItsWorkerIsJoined() => WithPipe(async (f, inspector) =>
    {
        var before = Files(f); var sent = f.SentSnapshot();
        await f.Runtime.DisposeAsync();
        await inspector.Completion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(inspector.Completion.IsCompletedSuccessfully);
        await inspector.DisposeAsync();
        Rejected(await f.Handler.InspectOwnedRuntimeAsync(f.Runtime, Query(f)));
        Unchanged(f, before, sent);
    });
}
