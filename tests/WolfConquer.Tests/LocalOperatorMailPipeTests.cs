using System.Buffers.Binary;
using System.IO.Pipes;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LocalOperatorMailPipeTests
{
    private static readonly Guid Domain = Guid.Parse("5cf0cd5a-e727-4d35-a6f3-6406ce3a01cf");
    private static readonly JsonSerializerOptions RequestJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static LegacyOperatorGearMailRequest Request() => new(Guid.NewGuid(), Domain, 10, 10,
        "Gear", "Operator", "Local test", [new(410003)]);

    [Fact]
    public void ValidRequestHasTypedFieldsAndCanonicalBoundedName()
    {
        var expected = Request();
        var parsed = LocalOperatorMailPipe.ParseRequest(JsonSerializer.SerializeToUtf8Bytes(expected, RequestJson));
        Assert.Equal(expected.OperationId, parsed.OperationId);
        Assert.Equal(expected.ExpectedDomain, parsed.ExpectedDomain);
        Assert.Equal(expected.Items, parsed.Items);
        var name = LocalOperatorMailPipe.NameFor(Domain);
        Assert.Equal("wolfconquer-operator-mail-5cf0cd5ae7274d35a6f36406ce3a01cf", name);
        Assert.True(LocalOperatorMailPipe.IsOperatorPipeName(name));
        Assert.False(LocalOperatorMailPipe.IsOperatorPipeName(name.ToUpperInvariant()));
        Assert.False(LocalOperatorMailPipe.IsOperatorPipeName("\\\\server\\pipe\\" + name));
        Assert.Throws<ArgumentException>(() => LocalOperatorMailPipe.NameFor(Guid.Empty));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("nestedDuplicate")]
    [InlineData("unknown")]
    [InlineData("nestedUnknown")]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("empty")]
    [InlineData("wrongCase")]
    public void MalformedRequestsAreRejected(string kind)
    {
        var json = JsonSerializer.Serialize(Request(), RequestJson);
        json = kind switch
        {
            "duplicate" => "{\"accountId\":10," + json[1..],
            "nestedDuplicate" => json.Replace("\"itemId\":410003", "\"itemId\":410003,\"itemId\":420003", StringComparison.Ordinal),
            "unknown" => "{\"password\":\"synthetic-sentinel\"," + json[1..],
            "nestedUnknown" => json.Replace("\"itemId\":410003", "\"itemId\":410003,\"uid\":7000", StringComparison.Ordinal),
            "missing" => json.Replace("\"content\":\"Local test\",", "", StringComparison.Ordinal),
            "null" => json[..json.IndexOf("\"items\":", StringComparison.Ordinal)] + "\"items\":null}",
            "empty" => json[..json.IndexOf("\"items\":", StringComparison.Ordinal)] + "\"items\":[]}",
            _ => json.Replace("\"operationId\"", "\"OperationId\"", StringComparison.Ordinal),
        };
        Assert.Throws<JsonException>(() => LocalOperatorMailPipe.ParseRequest(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void OversizeAndNonobjectRequestsAreRejected()
    {
        Assert.Throws<JsonException>(() => LocalOperatorMailPipe.ParseRequest(new byte[LocalOperatorMailPipe.MaximumRequestBytes + 1]));
        Assert.Throws<JsonException>(() => LocalOperatorMailPipe.ParseRequest("[]"u8.ToArray()));
    }

    [Fact]
    public void ResultRoundtripRetainsCommittedBoundaryAndRejectsNumericStatus()
    {
        var expected = new LegacyOperatorGearMailResult(Guid.NewGuid(),
            LegacyOperatorGearMailStatus.CommittedNotificationStopped, "postcommit", [1], [7001]);
        var json = LocalOperatorMailPipe.FormatResult(expected);
        var actual = LocalOperatorMailPipe.ParseResult(Encoding.UTF8.GetBytes(json));
        Assert.Equal(expected.OperationId, actual.OperationId);
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.ItemUids, actual.ItemUids);
        Assert.Throws<JsonException>(() => LocalOperatorMailPipe.ParseResult(Encoding.UTF8.GetBytes(
            json.Replace("\"CommittedNotificationStopped\"", "3", StringComparison.Ordinal))));
    }

    [Fact]
    public async Task ActualCurrentUserPipeDeliversThroughRealFrameAndJoinsWorker()
    {
        if (!OperatingSystem.IsWindows()) return;
        await LegacyOperatorGearMailOwnerTests.WithOwnerAsync(async fixture =>
        {
            var previous = Environment.GetEnvironmentVariable("WOLFCONQUER_OPERATOR_MAIL");
            try
            {
                Environment.SetEnvironmentVariable("WOLFCONQUER_OPERATOR_MAIL", "1");
                var endpoint = Assert.IsType<LocalOperatorMailPipe>(LocalOperatorMailPipe.StartIfEnabled(
                    fixture.Handler, fixture.Runtime, fixture.Domain));
                Exception? primary = null;
                try
                {
                var request = fixture.Request(Guid.NewGuid());
                var response = await ExchangeAsync(endpoint.PipeName, request);
                Assert.Equal(request.OperationId, response.OperationId);
                Assert.True(response.Status == LegacyOperatorGearMailStatus.CommittedNotificationCompleted, response.Reason);
                Assert.Equal(Assert.Single(response.ItemUids), Assert.Single(fixture.Items().WarehouseItems).Uid);
                Assert.Equal(Assert.Single(response.MailIds), Assert.Single(fixture.Mail()).Id);
                var water = fixture.HighWater;
                var repeated = await ExchangeAsync(endpoint.PipeName, request);
                Assert.Equal(LegacyOperatorGearMailStatus.PreviouslySubmittedOutcomeUnproven, repeated.Status);
                Assert.Equal(water, fixture.HighWater);
                Assert.Single(fixture.Items().WarehouseItems);
                }
                catch (Exception error) { primary = error; }
                try { await endpoint.DisposeAsync(); }
                catch (Exception cleanup)
                {
                    if (primary is not null) throw new AggregateException(primary, cleanup);
                    throw;
                }
                if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
                Assert.True(endpoint.Completion.IsCompleted);
            }
            finally { Environment.SetEnvironmentVariable("WOLFCONQUER_OPERATOR_MAIL", previous); }
        });
    }

    private static async Task<LegacyOperatorGearMailResult> ExchangeAsync(string name, LegacyOperatorGearMailRequest request)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var peer = new NamedPipeClientStream(".", name, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await peer.ConnectAsync(deadline.Token);
        var body = JsonSerializer.SerializeToUtf8Bytes(request, RequestJson);
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, body.Length);
        await peer.WriteAsync(header, deadline.Token);
        await peer.WriteAsync(body, deadline.Token);
        await peer.FlushAsync(deadline.Token);
        await peer.ReadExactlyAsync(header, deadline.Token);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        Assert.InRange(length, 1, LocalOperatorMailPipe.MaximumResponseBytes);
        var response = new byte[length];
        await peer.ReadExactlyAsync(response, deadline.Token);
        return LocalOperatorMailPipe.ParseResult(response);
    }
}
