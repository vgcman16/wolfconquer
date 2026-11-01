using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlPhysicalLifecycleTests
{
    private sealed record FlashingGuardRow(LegacyNativeStatusFlagsBigVector32.Flag Row,
        int Key, int Seconds, int InvokerSeconds, DateTime Timer, DateTime InvokerTimer, bool RemoveOnDead);

    private sealed record FlashingGuardState(WorldSession ActorSession, WorldSession TargetSession,
        LegacyOrdinarySpellBinding ActorBinding, LegacyOrdinarySpellBinding TargetBinding,
        LegacyCanAttackFlashingRegistration Registration, object FlagMap, uint[] Bits, uint[] BitValues,
        FlashingGuardRow[] Rows, object SpellMap, object[] SpellRows, object ProficiencyMap,
        object[] ProficiencyRows, object[] RuneRows, LegacyNativeInteractQuery Repeat, MsgAttackQuery RepeatValues, bool OnAutoAttack,
        DateTime AttackStamp, int Hitpoints, uint Mana, ushort Stamina, object Action,
        Dictionary<string, byte[]> Files, byte[][] ActorOutput, byte[][] TargetOutput);

    private static LegacyNativeInteractQuery FlashingGuardRepeat(LegacyNativeGameClientRuntime client) =>
        Assert.IsType<LegacyNativeInteractQuery>(typeof(LegacyNativeGameClientRuntime)
            .GetField("autoAttack", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(client));

    // Observe the real descriptor as DATA; never construct or publish native repeat state.
    private static MsgAttackQuery FlashingGuardRepeatValues(LegacyNativeInteractQuery query) =>
        new(query.UID, query.OpponentUID, query.X, query.Y, (MsgAttackType)query.AtkType, query.Damage,
            query.KilledMonster, query.ResponseDamage, (MsgAttackEffect)query.Effect, query.Unknown,
            query.CombatGear, query.SpellLevel, query.SpellID);

    private static FlashingGuardState CaptureFlashingGuardState(OwnedEquipmentBehaviorFixture f)
    {
        var actor = f.Current(Actor); var target = f.Current(Target);
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(actor.NativeGameClient!.OrdinarySpellBinding);
        var peer = Assert.IsType<LegacyOrdinarySpellBinding>(target.NativeGameClient!.OrdinarySpellBinding);
        Assert.True(LegacyNativeStatusFlagsBigVector32.HasOwnedDragonhowlFlashing(binding));
        Assert.True(LegacyCanAttackFlashingRegistration.TryRead(actor, out var registration));
        var vector = binding.Player.BitVector;
        var rows = vector.GetFlags().Select(row => new FlashingGuardRow(row, row.Key, row.Secounds,
            row.InvokerSecouds, row.Timer, row.TimerInvoker, row.RemoveOnDead)).ToArray();
        var flashing = Assert.Single(rows, row => row.Key == Flashing);
        Assert.Equal(registration.Timer, flashing.Timer); Assert.Equal(30, flashing.Seconds);
        Assert.True(flashing.RemoveOnDead); Assert.True(Flag(actor, Flashing));
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.True(f.Store.IsBoundSpellOwnerCurrent(peer));
        var repeat = FlashingGuardRepeat(binding.Client);
        return new(actor, target, binding, peer, registration, vector.ArrayFlags, vector.bits, vector.bits.ToArray(), rows,
            binding.Storage.ClientSpells, binding.Storage.ClientSpells.Values.Cast<object>().ToArray(),
            binding.Proficiencies.ClientProf, binding.Proficiencies.ClientProf.Values.Cast<object>().ToArray(),
            binding.Items.Rune.Objects.Cast<object>().ToArray(), repeat, FlashingGuardRepeatValues(repeat), binding.Client.OnAutoAttack,
            binding.Player.AttackStamp, binding.Player.HitPoints, binding.Player.Mana, binding.Player.Stamina,
            binding.Player.Action, f.Data.Files(), f.ActorFrames.Select(bytes => bytes.ToArray()).ToArray(),
            f.Frames(Target).Select(bytes => bytes.ToArray()).ToArray());
    }

    private static void AssertFlashingGuardState(OwnedEquipmentBehaviorFixture f, FlashingGuardState before,
        WorldSession? expectedActor = null)
    {
        var actor = f.Current(Actor); var binding = before.ActorBinding; var vector = binding.Player.BitVector;
        Assert.Same(expectedActor ?? before.ActorSession, actor); Assert.Same(before.TargetSession, f.Current(Target));
        Assert.Same(binding, actor.NativeGameClient!.OrdinarySpellBinding);
        Assert.Same(before.TargetBinding, f.Current(Target).NativeGameClient!.OrdinarySpellBinding);
        Assert.Equal(before.TargetSession.Character.MinHitPoints, before.TargetBinding.Player.HitPoints);
        Assert.Same(before.Registration, actor.CanAttackFlashingRegistration);
        Assert.True(LegacyNativeStatusFlagsBigVector32.HasOwnedDragonhowlFlashing(binding));
        Assert.True(Flag(actor, Flashing)); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(before.TargetBinding));
        Assert.False(binding.IsRetired); Assert.False(before.TargetBinding.IsRetired); Assert.False(f.Recovery.IsBlocked);
        Assert.Same(before.FlagMap, vector.ArrayFlags); Assert.Same(before.Bits, vector.bits);
        Assert.Equal(before.BitValues, vector.bits);
        var rows = vector.GetFlags(); Assert.Equal(before.Rows.Length, rows.Length);
        foreach (var saved in before.Rows)
        {
            var row = Assert.Single(rows, row => row.Key == saved.Key); Assert.Same(saved.Row, row);
            Assert.Equal(saved.Seconds, row.Secounds); Assert.Equal(saved.InvokerSeconds, row.InvokerSecouds);
            Assert.Equal(saved.Timer, row.Timer); Assert.Equal(saved.InvokerTimer, row.TimerInvoker);
            Assert.Equal(saved.RemoveOnDead, row.RemoveOnDead);
        }
        Assert.Same(before.SpellMap, binding.Storage.ClientSpells);
        Assert.Equal(before.SpellRows.Length, binding.Storage.ClientSpells.Count);
        foreach (var row in before.SpellRows) Assert.Contains(binding.Storage.ClientSpells.Values, value => ReferenceEquals(row, value));
        Assert.Same(before.ProficiencyMap, binding.Proficiencies.ClientProf);
        Assert.Equal(before.ProficiencyRows.Length, binding.Proficiencies.ClientProf.Count);
        foreach (var row in before.ProficiencyRows) Assert.Contains(binding.Proficiencies.ClientProf.Values, value => ReferenceEquals(row, value));
        var runes = binding.Items.Rune.Objects; Assert.Equal(before.RuneRows.Length, runes.Length);
        foreach (var row in before.RuneRows) Assert.Contains(runes, value => ReferenceEquals(row, value));
        Assert.Same(before.Repeat, FlashingGuardRepeat(binding.Client)); Assert.Equal(before.OnAutoAttack, binding.Client.OnAutoAttack);
        Assert.Equal(before.RepeatValues, FlashingGuardRepeatValues(before.Repeat));
        Assert.Equal(before.AttackStamp, binding.Player.AttackStamp); Assert.Equal(before.Hitpoints, binding.Player.HitPoints);
        Assert.Equal(before.Mana, binding.Player.Mana); Assert.Equal(before.Stamina, binding.Player.Stamina);
        Assert.Equal(before.Action, (object)binding.Player.Action);
        f.Data.AssertFiles(before.Files);
        Assert.Equal(before.ActorOutput.Length, f.ActorFrames.Count); Assert.Equal(before.TargetOutput.Length, f.Frames(Target).Count);
        for (var i = 0; i < before.ActorOutput.Length; i++) Assert.Equal(before.ActorOutput[i], f.ActorFrames[i]);
        for (var i = 0; i < before.TargetOutput.Length; i++) Assert.Equal(before.TargetOutput[i], f.Frames(Target)[i]);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }

    private static void AssertFlashingGuardBoundary(InvalidOperationException error)
    {
        Assert.Equal("ordinarySpellPublicationRequiresCurrentCompleteOwner", error.Message);
        Assert.Contains("WorldSessionStore.RequireOwnedDragonhowlFlashingTransition", error.StackTrace ?? string.Empty,
            StringComparison.Ordinal); // A prior root, admission or graph guard is not this regression.
    }
}
