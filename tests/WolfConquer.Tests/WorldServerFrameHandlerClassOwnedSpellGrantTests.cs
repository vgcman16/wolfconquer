using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    public static TheoryData<long> UnprovenLevel110ClassGrantCases()
    {
        return new TheoryData<long>
        {
            2_000,
            4_000,
            5_000,
            6_000,
            7_000,
            8_000,
            13_002,
            14_002,
            16_000
        };
    }

    [Theory]
    [MemberData(nameof(UnprovenLevel110ClassGrantCases))]
    public async Task HandlerLegacyWorldLoginDoesNotGrantUnprovenLevel110ClassSpellsOrPurgeLearnedSpell(
        long classCode)
    {
        var usersRoot = Path.Combine(_usersRoot, "Users");
        Directory.CreateDirectory(usersRoot);
        WriteSnapshot(
            usersRoot,
            1293600,
            uid: 1293600,
            classCode: classCode,
            level: 110,
            reborn: 0,
            secondClass: 0);
        var loader = new CharacterSnapshotLoader(usersRoot);
        var playerSpellsRoot = Path.Combine(_usersRoot, "PlayersSpells");
        Directory.CreateDirectory(playerSpellsRoot);
        var learnedSpell = PlayerSpellSnapshotLoaderTests.Entry(
            6_001,
            level: 4,
            experience: 1_111,
            previousLevel: 2,
            soulLevel: 3,
            useSpellSoul: 1);
        var originalBytes = PlayerSpellSnapshotLoaderTests.BuildPlayerSpellRecords([learnedSpell]);
        var playerSpellsPath = Path.Combine(playerSpellsRoot, "1293600.bin");
        File.WriteAllBytes(playerSpellsPath, originalBytes);
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(loader, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(1293600).ToFrameBytes()));

        var persistedSpells = loader.LoadPlayerSpellsByCharacterUid(1293600);
        Assert.Equal(new[] { learnedSpell }, persistedSpells.Spells);
        Assert.Equal(originalBytes, File.ReadAllBytes(playerSpellsPath));
        Assert.Equal(
            new[] { learnedSpell },
            GetSession(handler, connection).PlayerSpells.Spells);
        Assert.DoesNotContain("legacy Trojan-lineage login spell grant", log.ToString());
    }

    [Theory]
    [InlineData(1_000, 0)]
    [InlineData(1_051, 0)]
    [InlineData(2_000, 1_000)]
    [InlineData(2_000, 1_051)]
    public async Task HandlerLegacyWorldLoginAppendsMatchingTrojanLineageCycloneAndPreservesLearnedSpell(
        long classCode,
        long secondClass)
    {
        var usersRoot = Path.Combine(_usersRoot, "Users");
        Directory.CreateDirectory(usersRoot);
        WriteSnapshot(
            usersRoot,
            1293600,
            uid: 1293600,
            classCode: classCode,
            level: 110,
            reborn: 0,
            secondClass: secondClass);
        var loader = new CharacterSnapshotLoader(usersRoot);
        var playerSpellsRoot = Path.Combine(_usersRoot, "PlayersSpells");
        Directory.CreateDirectory(playerSpellsRoot);
        var learnedSpell = PlayerSpellSnapshotLoaderTests.Entry(
            6_001,
            level: 4,
            experience: 1_111,
            previousLevel: 2,
            soulLevel: 3,
            useSpellSoul: 1);
        File.WriteAllBytes(
            Path.Combine(playerSpellsRoot, "1293600.bin"),
            PlayerSpellSnapshotLoaderTests.BuildPlayerSpellRecords([learnedSpell]));
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(loader, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(1293600).ToFrameBytes()));

        var cyclone = new PlayerSpellSnapshotEntry(
            LegacyClassOwnedSpellGrantProfile.CycloneSpellId,
            Level: 0,
            Experience: 0,
            PreviousLevel: 0,
            SoulLevel: 0,
            UseSpellSoul: 0);
        var expectedSpells = new[] { learnedSpell, cyclone };
        var persistedSpells = loader.LoadPlayerSpellsByCharacterUid(1293600);
        Assert.Equal(expectedSpells, persistedSpells.Spells);
        Assert.Equal(expectedSpells, GetSession(handler, connection).PlayerSpells.Spells);
        Assert.Contains(
            "WorldServer applied legacy Trojan-lineage login spell grant uid=1293600",
            log.ToString());
    }

    [Fact]
    public async Task HandlerLegacyWorldLoginDoesNotOverwriteExistingCycloneOrPurgeOtherLearnedSpell()
    {
        var usersRoot = Path.Combine(_usersRoot, "Users");
        Directory.CreateDirectory(usersRoot);
        WriteSnapshot(
            usersRoot,
            1293600,
            uid: 1293600,
            classCode: 1_000,
            level: 110,
            reborn: 0,
            secondClass: 0);
        var loader = new CharacterSnapshotLoader(usersRoot);
        var playerSpellsRoot = Path.Combine(_usersRoot, "PlayersSpells");
        Directory.CreateDirectory(playerSpellsRoot);
        var learnedSpell = PlayerSpellSnapshotLoaderTests.Entry(
            6_001,
            level: 4,
            experience: 1_111,
            previousLevel: 2,
            soulLevel: 3,
            useSpellSoul: 1);
        var learnedCyclone = PlayerSpellSnapshotLoaderTests.Entry(
            LegacyClassOwnedSpellGrantProfile.CycloneSpellId,
            level: 6,
            experience: 2_222,
            previousLevel: 5,
            soulLevel: 4,
            useSpellSoul: 2);
        var expectedSpells = new[] { learnedSpell, learnedCyclone };
        var originalBytes = PlayerSpellSnapshotLoaderTests.BuildPlayerSpellRecords(expectedSpells);
        var playerSpellsPath = Path.Combine(playerSpellsRoot, "1293600.bin");
        File.WriteAllBytes(playerSpellsPath, originalBytes);
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(loader, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(1293600).ToFrameBytes()));

        var persistedSpells = loader.LoadPlayerSpellsByCharacterUid(1293600);
        Assert.Equal(expectedSpells, persistedSpells.Spells);
        Assert.Equal(originalBytes, File.ReadAllBytes(playerSpellsPath));
        Assert.Equal(expectedSpells, GetSession(handler, connection).PlayerSpells.Spells);
        Assert.DoesNotContain("legacy Trojan-lineage login spell grant", log.ToString());
    }
}
