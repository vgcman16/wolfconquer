using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 3)]
    public async Task WireObservedRawPrefixesRemainDistinctWhileMetadataDedupesStructure(bool raw, int expectedRecords)
    {
        await WithWireCurrentSessionAsync((owner, sessions) =>
        {
            WorldWireDiagnosticTestFiles.Run(files =>
            {
                var logger = files.Open(new() { ProtobufOpcodes = [2419], RawPayloadOpcodes = raw ? [2419] : [], MaxRawPayloadBytes = 2 });
                // All observations have identical tags, wire types, spans,
                // lengths and opcode. Distinct prefixes, repeats and one
                // same-prefix change beyond the cap are exercised separately.
                byte[][] payloads =
                [
                    [0x08, 0, 0x48, 1], [0x08, 1, 0x48, 1], [0x08, 0, 0x48, 1],
                    [0x08, 0, 0x48, 2], // differs only beyond the observed prefix
                    [0x08, 2, 0x48, 1],
                ];
                foreach (var payload in payloads)
                    logger.RecordPacket(ConquerProtocolProfile.VisibleDx8D13,
                        UnknownPacketAndFieldLoggerTests.Frame(2419, payload, segmented: true), false, owner.Connection!, sessions);
                var records = files.Read(logger);
                Assert.Equal(expectedRecords, records.Length);
                var structure = records[0].GetProperty("fields").GetRawText();
                Assert.All(records, record =>
                {
                    Assert.Equal(2419, record.GetProperty("opcode").GetInt32());
                    Assert.Equal(4, record.GetProperty("payloadLength").GetInt32());
                    Assert.Equal("complete", record.GetProperty("scanStatus").GetString());
                    Assert.Equal(structure, record.GetProperty("fields").GetRawText());
                    UnknownPacketAndFieldLoggerTests.AssertRecordKeys(record);
                });
                if (raw)
                {
                    Assert.Equal(new[] { "0800", "0801", "0802" }, records.Select(record => record.GetProperty("rawPayloadHex").GetString()));
                    Assert.All(records, record => Assert.True(record.GetProperty("rawPayloadTruncated").GetBoolean()));
                }
                else
                {
                    AssertRawAbsent(Assert.Single(records));
                }
            });
            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData("records", 2)]
    [InlineData("signatures", 2)]
    [InlineData("rate", 1)]
    public async Task WireDistinctRawPrefixesRespectRecordRateAndSignatureBudgets(string budget, int expectedRecords)
    {
        await WithWireCurrentSessionAsync((owner, sessions) =>
        {
            WorldWireDiagnosticTestFiles.Run(files =>
            {
                var options = new UnknownPacketAndFieldLoggerOptions { RawPayloadOpcodes = [2419], MaxRawPayloadBytes = 2 };
                options = budget switch
                {
                    "records" => options with { MaxRecords = 2 }, "signatures" => options with { MaxDistinctSignatures = 2 },
                    "rate" => options with { MaxRecordsPerMinute = 1 }, _ => throw new ArgumentOutOfRangeException(nameof(budget)),
                };
                var logger = files.Open(options);
                for (byte value = 0; value < 4; value++)
                    logger.RecordPacket(ConquerProtocolProfile.VisibleDx8D13,
                        UnknownPacketAndFieldLoggerTests.Frame(2419, [0x08, value, 0x48, 1]), false, owner.Connection!, sessions);
                var records = files.Read(logger);
                Assert.Equal(expectedRecords, records.Length);
                Assert.Equal(new[] { "0800", "0801" }.Take(expectedRecords),
                    records.Select(record => record.GetProperty("rawPayloadHex").GetString()));
                Assert.All(records, record => Assert.True(record.GetProperty("rawPayloadTruncated").GetBoolean()));
            });
            return Task.CompletedTask;
        });
    }

    private async Task WithWireCurrentSessionAsync(Func<TargetOwnedSpellStartupOwner, WorldSessionStore, Task> body)
    {
        var owner = new TargetOwnedSpellStartupOwner();
        Exception? primary = null;
        try
        {
            var users = owner.CreateUsers();
            WriteSnapshot(users, 1293600, uid: 1293600);
            owner.Bind(new CharacterSnapshotLoader(users));
            await owner.Handler!.HandleFrameAsync(owner.Connection!, ReadFrame(new LegacyMsgLoginGamePacket(1293600).ToFrameBytes()));
            owner.Admission!.AssertCommitObserved();
            var current = GetSession(owner.Handler, owner.Connection!);
            var saved = WireSavedFiles(owner.Root);
            var sentCount = owner.Sent.Count;
            await body(owner, GetSessions(owner.Handler));
            Assert.Same(current, GetSession(owner.Handler, owner.Connection!));
            Assert.Equal(sentCount, owner.Sent.Count);
            var after = WireSavedFiles(owner.Root);
            Assert.Equal(saved.Keys.Order(StringComparer.OrdinalIgnoreCase), after.Keys.Order(StringComparer.OrdinalIgnoreCase));
            foreach (var (path, bytes) in saved) Assert.Equal(bytes, after[path]);
        }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            try { await owner.DisposeAsync(); }
            catch (Exception cleanup)
            {
                if (primary is null) throw;
                if (!ReferenceEquals(primary, cleanup)) throw new AggregateException(primary, cleanup);
            }
        }
    }
}
