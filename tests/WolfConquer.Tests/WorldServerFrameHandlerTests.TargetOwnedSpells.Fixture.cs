using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    private async Task RequireTargetOwnedSpellStartup(long current, long first, long second,
        long runtimeHitpoints, bool existingCyclone, bool expectedGrant)
    {
        var owner = new TargetOwnedSpellStartupOwner();
        Exception? primary = null;
        try { await RunTargetOwnedSpellStartup(owner, current, first, second, runtimeHitpoints, existingCyclone, expectedGrant); }
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

    private async Task RunTargetOwnedSpellStartup(TargetOwnedSpellStartupOwner owner, long current,
        long first, long second, long runtimeHitpoints, bool existingCyclone, bool expectedGrant)
    {
        var users = owner.CreateUsers();
        WriteSnapshot(users, 1293600, uid: 1293600, classCode: current, level: 110,
            reborn: 0, secondClass: second);
        var characterPath = Path.Combine(users, "1293600.ini");
        var characterText = File.ReadAllText(characterPath);
        Assert.Contains("FirstClass=0", characterText);
        File.WriteAllText(characterPath, characterText.Replace("FirstClass=0", $"FirstClass={first}", StringComparison.Ordinal));
        var learned = PlayerSpellSnapshotLoaderTests.Entry(6001, level: 4, experience: 1111,
            previousLevel: 2, soulLevel: 3, useSpellSoul: 1);
        var cyclone = PlayerSpellSnapshotLoaderTests.Entry(1110, level: 6, experience: 2222,
            previousLevel: 5, soulLevel: 4, useSpellSoul: 2);
        var beforeRows = existingCyclone ? new[] { learned, cyclone } : new[] { learned };
        var beforeBytes = PlayerSpellSnapshotLoaderTests.BuildPlayerSpellRecords(beforeRows);
        beforeBytes[4 + PlayerSpellSnapshotLoader.LegacySpellSize - 1] = 0xA5;
        if (existingCyclone) beforeBytes[^1] = 0x5A;
        var spellsRoot = Path.Combine(owner.Root, "PlayersSpells");
        Directory.CreateDirectory(spellsRoot);
        var spellPath = Path.Combine(spellsRoot, "1293600.bin");
        File.WriteAllBytes(spellPath, beforeBytes);
        var loader = new CharacterSnapshotLoader(users);
        owner.Bind(loader);
        var log = owner.Log;
        var admission = owner.Admission!;
        var handler = owner.Handler!;
        var sent = owner.Sent;
        var connection = owner.Connection!;

        await handler.HandleFrameAsync(connection, ReadFrame(new LegacyMsgLoginGamePacket(1293600).ToFrameBytes()));

        var granted = new PlayerSpellSnapshotEntry(1110, 0, 0, 0, 0, 0);
        var expected = expectedGrant ? new[] { learned, granted } : beforeRows;
        Assert.Equal(expected, loader.LoadPlayerSpellsByCharacterUid(1293600).Spells);
        var session = GetSession(handler, connection);
        Assert.Equal(expected, session.PlayerSpells.Spells);
        Assert.Equal((current, first, second, 110L, 0L),
            (session.Character.Class, session.Character.FirstClass, session.Character.SecondClass,
                session.Character.Level, session.Character.Reborn));
        var afterBytes = File.ReadAllBytes(spellPath);
        if (expectedGrant)
        {
            Assert.Equal(beforeRows.Length + 1, BinaryPrimitives.ReadInt32LittleEndian(afterBytes));
            Assert.Equal(beforeBytes.Length + 12, afterBytes.Length);
            Assert.Equal(beforeBytes.AsSpan(4).ToArray(), afterBytes.AsSpan(4, beforeBytes.Length - 4).ToArray());
            Assert.Equal(new byte[] { 0x56, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, afterBytes.AsSpan(beforeBytes.Length).ToArray());
        }
        else Assert.Equal(beforeBytes, afterBytes);
        Assert.Contains("WorldServer ordinary admission committed key=1293600.", log.ToString());
        await admission.AssertCommittedAndRepeatedAsync(handler, connection, sent, expectedRuntimeHitpoints: runtimeHitpoints);
        Assert.Equal(afterBytes, File.ReadAllBytes(spellPath));
    }
}
