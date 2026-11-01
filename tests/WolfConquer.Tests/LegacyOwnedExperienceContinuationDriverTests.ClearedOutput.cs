using System.Text;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedExperienceContinuationDriverTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualFinalExperienceOutputLossAfterClearReconnectsWithoutRecredit(bool eof) => Run(async (f, monster, catalog) =>
    {
        var before = f.Current();
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
        var expected = f.Data.Files();
        var key = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
        var text = Encoding.UTF8.GetString(expected[key]);
        text = Replace(text, "Level=137", "Level=139");
        text = Replace(text, "Atributes=12", "Atributes=18");
        text = Replace(text, "MinHitPoints=5000", "MinHitPoints=11136");
        f.ActorFrames.Clear();
        Dictionary<string, byte[]>? atOutput = null;
        WorldSession? committed = null;
        byte[]? attempted = null;
        Task? close = null;
        int interruptions = 0;
        f.BeforeSend = (recipient, bytes) =>
        {
            if (recipient != OwnedEquipmentBehaviorFixture.Actor || binding.Player.Level != 139 ||
                !LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var update) ||
                !update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Experience)) return;
            interruptions++;
            attempted = bytes.ToArray();
            atOutput = f.Data.Files();
            f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out committed);
            if (eof) close = f.Connection(OwnedEquipmentBehaviorFixture.Actor).CompleteTransportCloseAsync();
            throw new IOException(eof ? "owned final EXP transport EOF" : "owned final EXP output failure");
        };
        try { await ExpectHitFailure(f, monster); }
        finally { f.BeforeSend = null; }
        Assert.Equal(1, interruptions); Assert.NotNull(atOutput); Assert.NotNull(attempted); Assert.NotNull(committed);
        if (eof) { Assert.NotNull(close); await close; }

        var attacks = f.ActorFrames.Select(OwnedEquipmentBehaviorFixture.ReadFrame)
            .Select(frame => MsgAttackPacket.TryParse(frame, out var packet) ? packet : (MsgAttackPacket?)null)
            .Where(packet => packet is { } value && value.Query.AttackType == MsgAttackType.Physical && value.Query.OpponentUid == monster.Uid)
            .Select(packet => packet!.Value).ToArray();
        var hit = Assert.Single(attacks);
        Assert.True(hit.Query.Damage > 0);
        // Same-level monster, rate one, no modifiers, and two actual thresholds of one.
        var earned = checked((ulong)Math.Max(10, hit.Query.Damage));
        Assert.True(earned > 2 && earned - 2 < 1_000_000_000);
        var finalExperience = earned - 2;
        text = Replace(text, "Experience=0", "Experience=" + finalExperience.ToString(System.Globalization.CultureInfo.InvariantCulture));
        expected[key] = Encoding.UTF8.GetBytes(text);
        Assert.Equal(139, committed.Character.Level); Assert.Equal(checked((long)finalExperience), committed.Character.Experience);
        Assert.Equal(18, committed.Character.AttributePoints); Assert.Equal(11136, committed.Character.MinHitPoints);
        Assert.Equal((ushort)139, binding.Player.Level); Assert.Equal(finalExperience, binding.Player.Experience);
        Assert.Equal((ushort)18, binding.Player.Atributes); Assert.Equal(11136, binding.Player.HitPoints);
        Assert.Equal(Update(OwnedEquipmentBehaviorFixture.Actor, LegacyMsgUpdateDataType.Experience, finalExperience), attempted);
        Assert.Null(LegacyExperienceContinuationCodec.ReadCharacterImage(atOutput[key], before.AccountId, checked((uint)before.Character.Uid)));
        Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), atOutput.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, atOutput[path]);
        f.Data.AssertFiles(expected);
        Assert.False(f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out _));
        Assert.True(binding.IsRetired); Assert.False(f.Recovery.IsBlocked); Assert.False(binding.Client.OnAutoAttack);
        Assert.DoesNotContain(f.ActorFrames, row => row.AsSpan().SequenceEqual(attempted));
        Assert.DoesNotContain("WorldServer sent legacy monster experience ", f.Log.ToString(), StringComparison.Ordinal);
        int firstLevel = f.ActorFrames.FindIndex(row => row.AsSpan().SequenceEqual(
            Update(OwnedEquipmentBehaviorFixture.Actor, LegacyMsgUpdateDataType.Level, 138)));
        int secondLevel = f.ActorFrames.FindIndex(row => row.AsSpan().SequenceEqual(
            Update(OwnedEquipmentBehaviorFixture.Actor, LegacyMsgUpdateDataType.Level, 139)));
        Assert.True(firstLevel >= 0 && secondLevel > firstLevel);
        Assert.Contains(f.ActorFrames.Skip(firstLevel + 1).Take(secondLevel - firstLevel - 1), row =>
            OwnedEquipmentBehaviorFixture.ReadFrame(row).Header.Opcode == LegacyPlayerStatusPacket.Opcode);
        Assert.Contains(f.ActorFrames.Skip(secondLevel + 1), row =>
            OwnedEquipmentBehaviorFixture.ReadFrame(row).Header.Opcode == LegacyPlayerStatusPacket.Opcode);

        if (eof) await f.ReconnectContinuationAfterAwaitedCloseAsync();
        else await f.ReconnectContinuationAsync();
        var resumed = f.Current();
        var fresh = Assert.IsType<LegacyOrdinarySpellBinding>(resumed.NativeGameClient!.OrdinarySpellBinding);
        Assert.NotSame(binding, fresh); Assert.True(f.Store.IsBoundSpellOwnerCurrent(fresh));
        Assert.Equal(139, resumed.Character.Level); Assert.Equal(checked((long)finalExperience), resumed.Character.Experience);
        Assert.Equal(18, resumed.Character.AttributePoints); Assert.Equal(11136, resumed.Character.MinHitPoints);
        Assert.Equal((ushort)139, fresh.Player.Level); Assert.Equal(finalExperience, fresh.Player.Experience);
        Assert.Null(LegacyExperienceContinuationCodec.ReadCharacterImage(File.ReadAllBytes(f.CharacterPath()),
            resumed.AccountId, checked((uint)resumed.Character.Uid)));
        Assert.DoesNotContain(f.ActorFrames, row => MsgAttackPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(row), out var packet) &&
            packet.Query.AttackType == MsgAttackType.Physical && packet.Query.OpponentUid == monster.Uid);
        f.Data.AssertFiles(expected);
    }, initialLevel: 137);
}
