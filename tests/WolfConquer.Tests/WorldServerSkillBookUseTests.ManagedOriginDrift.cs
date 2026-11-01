using System.Reflection;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerSkillBookUseTests
{
    [Fact]
    public async Task ExternalCharacterChangeAfterSnapshotResponseRefusesManagedStartup()
    {
        var users = Path.Combine(_dbRoot, "Users");
        var items = Path.Combine(_dbRoot, "PlayersItems");
        var spells = Path.Combine(_dbRoot, "PlayersSpells");
        foreach (var route in new[] { users, items, spells }) Directory.CreateDirectory(route);
        WriteSnapshot(users, 1_293_600, 1_293_600);
        var characterPath = Path.Combine(users, "1293600.ini");
        File.WriteAllBytes(Path.Combine(items, "1293600.bin"), PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            [PlayerItemSnapshotLoaderTests.DbItem(2001, 725016, 0, stackSize: 1)], []));
        File.WriteAllBytes(Path.Combine(spells, "1293600.bin"), PlayerSpellSnapshotLoaderTests.BuildPlayerSpells([6001]));
        var expectedFiles = CaptureHpMirrorFiles(_dbRoot);
        var characterKey = Path.GetRelativePath(_dbRoot, characterPath);
        var before = expectedFiles[characterKey];
        var changed = PatchUniqueAscii(before, "Money=98602439956", "Money=98602439957");
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(users), handoff, log);
        var sessions = Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
            .GetField("_sessions", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(handler));
        var connection = new ConquerTcpConnection(null, null, (bytes, _) =>
        {
            var frame = bytes.ToArray();
            sent.Add(frame);
            if (sent.Count == 1)
            {
                Assert.True(PacketReader.TryRead(frame, out var reader));
                Assert.Equal(WorldSnapshotWireStatus.Success, WorldSnapshotResponsePacket.ReadStatus(reader));
                Assert.Equal(1_293_600, WorldSnapshotResponsePacket.ReadAccountId(reader));
                Assert.Equal(1_293_600, WorldSnapshotResponsePacket.ReadUid(reader));
                Assert.Equal(before, File.ReadAllBytes(characterPath));
                File.WriteAllBytes(characterPath, changed);
                expectedFiles[characterKey] = changed;
            }
            return ValueTask.CompletedTask;
        });
        var ticket = handoff.Issue(1_293_600, "startup-drift", ConquerRuntimeDefaults.WorldServer);
        await handler.HandleFrameAsync(connection, ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));

        // The snapshot is a read response; no world-entry or item success may follow it.
        Assert.True(sent.Count == 1, log.ToString());
        Assert.False(sessions.TryGet(connection, out _));
        AssertHpMirrorFiles(expectedFiles, CaptureHpMirrorFiles(_dbRoot));
    }
}
