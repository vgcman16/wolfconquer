using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public async Task HandlerLegacyWorldLoginDoesNotGrantThunderStrikerLineageSparkShield()
    {
        var usersRoot = Path.Combine(_usersRoot, "Users");
        Directory.CreateDirectory(usersRoot);
        WriteSnapshot(
            usersRoot,
            1293601,
            uid: 1293601,
            classCode: 9_005,
            level: 70,
            reborn: 1,
            secondClass: 0);
        var loader = new CharacterSnapshotLoader(usersRoot);
        var playerSpellsRoot = Path.Combine(_usersRoot, "PlayersSpells");
        Directory.CreateDirectory(playerSpellsRoot);
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(loader, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(1293601).ToFrameBytes()));

        var persistedSpells = loader.LoadPlayerSpellsByCharacterUid(1293601);
        Assert.Empty(persistedSpells.SpellIds);
        Assert.Equal(persistedSpells.SpellIds.ToArray(), GetSession(handler, connection).PlayerSpells.SpellIds.ToArray());
        Assert.DoesNotContain("ThunderStriker login spell", log.ToString());
    }

    [Fact]
    public async Task HandlerLegacyWorldLoginPreservesLearnedThunderStrikerSparkShield()
    {
        var usersRoot = Path.Combine(_usersRoot, "Users");
        Directory.CreateDirectory(usersRoot);
        WriteSnapshot(
            usersRoot,
            1293601,
            uid: 1293601,
            classCode: 9_005,
            level: 70,
            reborn: 1,
            secondClass: 0);
        var loader = new CharacterSnapshotLoader(usersRoot);
        var playerSpellsRoot = Path.Combine(_usersRoot, "PlayersSpells");
        Directory.CreateDirectory(playerSpellsRoot);
        var learnedSpells = new PlayerSpellSnapshot(
        [
            new PlayerSpellSnapshotEntry(
                Id: 15_760,
                Level: 3,
                Experience: 12_345,
                PreviousLevel: 2,
                SoulLevel: 1,
                UseSpellSoul: 1),
        ]);
        var spellLoader = new PlayerSpellSnapshotLoader(playerSpellsRoot);
        spellLoader.SaveByCharacterUid(1293601, learnedSpells);
        var spellPath = Path.Combine(playerSpellsRoot, "1293601.bin");
        var originalBytes = File.ReadAllBytes(spellPath);
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(loader, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(1293601).ToFrameBytes()));

        var persistedSpells = spellLoader.LoadByCharacterUid(1293601);
        Assert.Equal(learnedSpells.Spells, persistedSpells.Spells);
        Assert.Equal(learnedSpells.Spells, GetSession(handler, connection).PlayerSpells.Spells);
        Assert.Equal(originalBytes, File.ReadAllBytes(spellPath));
        Assert.DoesNotContain("ThunderStriker login spell", log.ToString());
    }
}
