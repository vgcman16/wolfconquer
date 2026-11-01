using System.Collections;
using System.Reflection;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedExperienceContinuationDriverTests
{
    private const BindingFlags EarnedFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static object? EarnedField(object owner, string name) =>
        (owner.GetType().GetField(name, EarnedFields) ?? throw new InvalidOperationException("Missing observed field: " + name)).GetValue(owner);

    // These observations never construct a queue entry, operation lease, hit
    // request, native owner or continuation authority.
    private static void AssertEarnedExclusiveQueue(OwnedEquipmentBehaviorFixture f, int count)
    {
        var barrier = Assert.IsType<WorldOperationBarrier>(EarnedField(f.Handler, "_worldOperations"));
        lock (Assert.IsType<object>(EarnedField(barrier, "_gate")))
        {
            Assert.True(Assert.IsType<bool>(EarnedField(barrier, "_writer")));
            Assert.Equal(0, Assert.IsType<int>(EarnedField(barrier, "_readers")));
            var queue = Assert.IsAssignableFrom<IEnumerable>(EarnedField(barrier, "_queue"));
            var entries = queue.Cast<object>().ToArray();
            Assert.Equal(count, entries.Length);
            foreach (var entry in entries)
                Assert.True(Assert.IsType<bool>(entry.GetType().GetProperty("Exclusive", EarnedFields)!.GetValue(entry)));
        }
    }

    private sealed record EarnedPhysicalSlot(object Slot, long Generation, DateTime Stamp, object Native,
        object? Attempt, object? Committed, object? Dragonhowl, object? Repeat);

    private static EarnedPhysicalSlot ObserveEarnedPhysicalSlot(OwnedEquipmentBehaviorFixture f, uint uid)
    {
        var table = EarnedField(f.Handler, "basicPhysicalSlots")!;
        object?[] arguments = [f.Connection(uid), null];
        Assert.True(Assert.IsType<bool>(table.GetType().GetMethod("TryGetValue")!.Invoke(table, arguments)));
        var slot = Assert.IsAssignableFrom<object>(arguments[1]);
        return new(slot, Assert.IsType<long>(EarnedField(slot, "Generation")),
            Assert.IsType<DateTime>(EarnedField(slot, "AttackStamp")), EarnedField(slot, "Native")!,
            EarnedField(slot, "Attempt"), EarnedField(slot, "Committed"), EarnedField(slot, "DragonhowlCommitted"),
            EarnedField(slot, "Repeat"));
    }

    private sealed record EarnedProficiencyRow(LegacyNativeMsgProficiency Row, uint Id, uint Level,
        uint Experience, uint Needed, byte Previous);
    private sealed record EarnedPlayerState(uint Uid, WorldSession Session, LegacyOrdinarySpellBinding Binding,
        object SavedSpells, object SavedProficiencies, object SpellMap, object ProficiencyMap,
        EarnedProficiencyRow[] Proficiencies, object FlagMap, uint[] Bits, uint[] BitValues,
        int Hitpoints, uint Mana, ushort Stamina, ushort Level, ulong Experience, ushort Attributes,
        DateTime AttackStamp, object Action, object? NativeRepeat, MsgAttackQuery? RepeatValues,
        bool AutoAttack, EarnedPhysicalSlot Slot);
    private sealed record EarnedPendingSnapshot(EarnedPlayerState[] Players, Dictionary<string, byte[]> Files,
        byte[][] ActorOutput, byte[][] PeerOutput, string Log);

    private static MsgAttackQuery EarnedRepeatValues(LegacyNativeInteractQuery row) =>
        new(row.UID, row.OpponentUID, row.X, row.Y, (MsgAttackType)row.AtkType, row.Damage,
            row.KilledMonster, row.ResponseDamage, (MsgAttackEffect)row.Effect, row.Unknown,
            row.CombatGear, row.SpellLevel, row.SpellID);

    private static EarnedPendingSnapshot CaptureEarnedPending(OwnedEquipmentBehaviorFixture f)
    {
        var players = new[] { EarnedActor, EarnedPeer }.Select(uid =>
        {
            var session = f.Current(uid);
            var b = Assert.IsType<LegacyOrdinarySpellBinding>(session.NativeGameClient!.OrdinarySpellBinding);
            Assert.Empty(b.Storage.ClientSpells); Assert.Empty(b.Player.BitVector.GetFlags());
            var profs = b.Proficiencies.ClientProf.Values.Select(row => new EarnedProficiencyRow(row,
                row.ID, row.Level, row.Experience, row.NeededExperience, row.PreviouseLevel)).ToArray();
            var repeat = EarnedField(b.Client, "autoAttack");
            return new EarnedPlayerState(uid, session, b, b.SavedSpells, b.SavedProficiencies,
                b.Storage.ClientSpells, b.Proficiencies.ClientProf, profs, b.Player.BitVector.ArrayFlags,
                b.Player.BitVector.bits, b.Player.BitVector.bits.ToArray(), b.Player.HitPoints, b.Player.Mana,
                b.Player.Stamina, b.Player.Level, b.Player.Experience, b.Player.Atributes, b.Player.AttackStamp,
                b.Player.Action, repeat, repeat is LegacyNativeInteractQuery query ? EarnedRepeatValues(query) : null,
                b.Client.OnAutoAttack, ObserveEarnedPhysicalSlot(f, uid));
        }).ToArray();
        return new(players, f.Data.Files(), f.ActorFrames.Select(row => row.ToArray()).ToArray(),
            f.Frames(EarnedPeer).Select(row => row.ToArray()).ToArray(), f.Log.ToString());
    }

    private static void AssertEarnedPendingUnchanged(OwnedEquipmentBehaviorFixture f, EarnedPendingSnapshot before)
    {
        foreach (var saved in before.Players)
        {
            var b = saved.Binding; var player = b.Player;
            Assert.Same(saved.Session, f.Current(saved.Uid));
            Assert.Same(b, f.Current(saved.Uid).NativeGameClient!.OrdinarySpellBinding);
            Assert.False(b.IsRetired); Assert.False(f.Recovery.IsBlocked);
            Assert.Same(saved.SavedSpells, b.SavedSpells); Assert.Same(saved.SavedProficiencies, b.SavedProficiencies);
            Assert.Same(saved.SpellMap, b.Storage.ClientSpells); Assert.Empty(b.Storage.ClientSpells);
            Assert.Same(saved.ProficiencyMap, b.Proficiencies.ClientProf);
            Assert.Equal(saved.Proficiencies.Length, b.Proficiencies.ClientProf.Count);
            foreach (var row in saved.Proficiencies)
            {
                Assert.True(b.Proficiencies.ClientProf.TryGetValue(row.Id, out var current)); Assert.Same(row.Row, current);
                Assert.Equal(row.Level, current.Level); Assert.Equal(row.Experience, current.Experience);
                Assert.Equal(row.Needed, current.NeededExperience); Assert.Equal(row.Previous, current.PreviouseLevel);
            }
            Assert.Same(saved.FlagMap, player.BitVector.ArrayFlags); Assert.Same(saved.Bits, player.BitVector.bits);
            Assert.Equal(saved.BitValues, player.BitVector.bits); Assert.Empty(player.BitVector.GetFlags());
            Assert.Equal(saved.Hitpoints, player.HitPoints); Assert.Equal(saved.Mana, player.Mana);
            Assert.Equal(saved.Stamina, player.Stamina); Assert.Equal(saved.Level, player.Level);
            Assert.Equal(saved.Experience, player.Experience); Assert.Equal(saved.Attributes, player.Atributes);
            Assert.Equal(saved.AttackStamp, player.AttackStamp); Assert.Equal(saved.Action, (object)player.Action);
            Assert.Equal(saved.AutoAttack, b.Client.OnAutoAttack); Assert.Same(saved.NativeRepeat, EarnedField(b.Client, "autoAttack"));
            if (saved.NativeRepeat is LegacyNativeInteractQuery repeat) Assert.Equal(saved.RepeatValues, EarnedRepeatValues(repeat));
            var slot = ObserveEarnedPhysicalSlot(f, saved.Uid);
            Assert.Same(saved.Slot.Slot, slot.Slot); Assert.Equal(saved.Slot.Generation, slot.Generation);
            Assert.Equal(saved.Slot.Stamp, slot.Stamp); Assert.Same(saved.Slot.Native, slot.Native);
            Assert.Same(saved.Slot.Attempt, slot.Attempt); Assert.Same(saved.Slot.Committed, slot.Committed);
            Assert.Same(saved.Slot.Dragonhowl, slot.Dragonhowl); Assert.Same(saved.Slot.Repeat, slot.Repeat);
        }
        f.Data.AssertFiles(before.Files); Assert.Equal(before.Log, f.Log.ToString());
        AssertEarnedOutput(before.ActorOutput, f.ActorFrames); AssertEarnedOutput(before.PeerOutput, f.Frames(EarnedPeer));
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }

    private static void AssertEarnedOutput(byte[][] before, List<byte[]> actual)
    {
        Assert.Equal(before.Length, actual.Count);
        for (int i = 0; i < before.Length; i++) Assert.Equal(before[i], actual[i]);
    }
}
