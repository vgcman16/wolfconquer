using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class UnknownPacketAndFieldLoggerTests
{
    [Fact]
    public async Task OptionalHandlerHookObservesUnknownFramesWithoutSendingOrInventingAHandler()
    {
        await WorldWireDiagnosticTestFiles.RunAsync(async files =>
        {
            var logger = files.Open(new() { ProtobufOpcodes = [2419] });
            using var log = new StringWriter();
            var sent = new List<byte[]>();
            var handler = new WorldServerFrameHandler(snapshotLoader: null, handoffService: null, worldData: null,
                runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled, log: log,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13, wireDiagnostics: logger);
            var connection = new ConquerTcpConnection(null, null,
                (bytes, _) => { sent.Add(bytes.ToArray()); return ValueTask.CompletedTask; });
            Exception? primary = null;
            try
            {
                await handler.HandleFrameAsync(connection, Frame(4999, [0x73, 0x65, 0x63, 0x72, 0x65, 0x74]));
                await handler.HandleFrameAsync(connection, Frame(2419, [0x08, 0, 0x48, 1]));
                Assert.Empty(sent);
                var records = files.Read(logger);
                Assert.Equal(new[] { 4999, 2419 }, records.Select(record => record.GetProperty("opcode").GetInt32()));
                Assert.All(records, record => Assert.True(record.GetProperty("unknownOpcode").GetBoolean()));
                Assert.All(records, AssertMetadataOnly);
                Assert.Equal("complete", records[1].GetProperty("scanStatus").GetString());
                var text = log.ToString();
                Assert.Contains("unknown world opcode", text);
                Assert.DoesNotContain("payloadSampleHex", text);
                Assert.DoesNotContain("736563726574", text, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception error) { primary = error; throw; }
            finally
            {
                try { await connection.CompleteTransportCloseAsync(); }
                catch (Exception cleanup)
                {
                    if (primary is null) throw;
                    if (!ReferenceEquals(primary, cleanup)) throw new AggregateException(primary, cleanup);
                }
            }
        });
    }
}
