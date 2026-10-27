using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public async Task WireRawCaptureRequiresTheActualCurrentOrdinaryWorldSession()
    {
        // Reuse the genuine saved-login/UID issuer and its confirmed-closure
        // owner. No session, native client or authorization result is injected.
        var owner = new TargetOwnedSpellStartupOwner();
        Exception? primary = null;
        try
        {
            var users = owner.CreateUsers();
            WriteSnapshot(users, 1293600, uid: 1293600);
            owner.Bind(new CharacterSnapshotLoader(users));
            var handler = owner.Handler!;
            var connection = owner.Connection!;
            var sessions = GetSessions(handler);
            await WorldWireDiagnosticTestFiles.RunAsync(async files =>
            {
                var allowlist = new List<ushort> { 2419 };
                var logger = files.Open(new() { RawPayloadOpcodes = allowlist, MaxRawPayloadBytes = 2 });
                allowlist.Clear();
                allowlist.Add(2179); // cannot alter the captured allowlist
                var frame = UnknownPacketAndFieldLoggerTests.Frame(2419, [0x08, 0, 0x48, 1]);
                logger.RecordPacket(ConquerProtocolProfile.VisibleDx8D13, frame, true, connection, sessions);
                await handler.HandleFrameAsync(connection, ReadFrame(new LegacyMsgLoginGamePacket(1293600).ToFrameBytes()));
                owner.Admission!.AssertCommitObserved();
                var current = GetSession(handler, connection);
                Assert.Equal(1293600, current.Character.Uid);
                var before = WireSavedFiles(owner.Root);
                var sentCount = owner.Sent.Count;

                logger.RecordPacket(ConquerProtocolProfile.VisibleDx8D13, frame, true, connection, sessions);
                logger.RecordPacket(ConquerProtocolProfile.RootD11_7899, frame, true, connection, sessions);
                logger.RecordPacket(ConquerProtocolProfile.VisibleDx8D13,
                    UnknownPacketAndFieldLoggerTests.Frame(2419, [0x08, 0, 0x48, 1, 0]), true,
                    new ConquerTcpConnection(null, null), sessions);
                var invalid = current with { AccountId = 0 };
                Assert.True(sessions.TryCompareAndSet(connection, current, invalid));
                logger.RecordPacket(ConquerProtocolProfile.VisibleDx8D13,
                    UnknownPacketAndFieldLoggerTests.Frame(2419, [0x08, 0, 0x48, 1, 0, 0]), true, connection, sessions);
                Assert.True(sessions.TryCompareAndSet(connection, invalid, current));
                logger.RecordPacket(ConquerProtocolProfile.VisibleDx8D13,
                    frame with { Header = frame.Header with { Length = 7 } }, true, connection, sessions);

                var records = files.Read(logger);
                Assert.Equal(6, records.Length);
                AssertRawAbsent(records[0]);
                Assert.Equal("0800", records[1].GetProperty("rawPayloadHex").GetString());
                Assert.True(records[1].GetProperty("rawPayloadTruncated").GetBoolean());
                foreach (var record in records.Skip(2)) AssertRawAbsent(record);
                Assert.All(records, record =>
                {
                    Assert.Empty(record.GetProperty("fields").EnumerateArray());
                    Assert.Equal(JsonValueKind.Null, record.GetProperty("schema").ValueKind);
                    Assert.Equal("not_requested", record.GetProperty("scanStatus").GetString());
                    Assert.DoesNotContain(record.EnumerateObject(), property => property.Name is "accountId" or "characterUid" or "username");
                });
                Assert.Same(current, GetSession(handler, connection));
                Assert.Equal(sentCount, owner.Sent.Count);
                var after = WireSavedFiles(owner.Root);
                Assert.Equal(before.Keys.Order(StringComparer.OrdinalIgnoreCase), after.Keys.Order(StringComparer.OrdinalIgnoreCase));
                foreach (var (path, bytes) in before) Assert.Equal(bytes, after[path]);
            });
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

    private static Dictionary<string, byte[]> WireSavedFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileName(path).Equals("owner.lock", StringComparison.Ordinal))
            .ToDictionary(path => path, File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);

    private static void AssertRawAbsent(JsonElement record)
    {
        Assert.Equal(JsonValueKind.Null, record.GetProperty("rawPayloadHex").ValueKind);
        Assert.False(record.GetProperty("rawPayloadTruncated").GetBoolean());
    }
}
