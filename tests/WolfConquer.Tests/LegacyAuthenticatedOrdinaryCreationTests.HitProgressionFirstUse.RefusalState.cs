using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    private sealed record FirstUseRefusalState(WorldSession Actor, WorldSession Target,
        LegacyOrdinarySpellBinding ActorBinding, LegacyOrdinarySpellBinding TargetBinding,
        LegacySavedProficiencyImage ProfsOrigin, LegacySavedSpellImage SpellsOrigin,
        object ProficiencyMap, object ActorSpells, object TargetSpells, uint[] ActorBits, uint[] TargetBits,
        uint[] ActorBitValues, uint[] TargetBitValues, object ActorFlagMap, object TargetFlagMap,
        DateTime Stamp, object Action, int Hp, uint Mana, ushort Stamina, Dictionary<string, byte[]> Files);

    private static FirstUseRefusalState CaptureFirstUseRefusal(FirstUseHitFixture f)
    {
        var actor = f.Actor; var target = f.Victim;
        var own = actor.NativeGameClient!.OrdinarySpellBinding!; var peer = target.NativeGameClient!.OrdinarySpellBinding!;
        Assert.Empty(own.Proficiencies.ClientProf); Assert.False(own.Client.OnAutoAttack);
        return new(actor, target, own, peer, own.SavedProficiencies, peer.SavedSpells,
            own.Proficiencies.ClientProf, own.Storage.ClientSpells, peer.Storage.ClientSpells,
            own.Player.BitVector.bits, peer.Player.BitVector.bits, own.Player.BitVector.bits.ToArray(),
            peer.Player.BitVector.bits.ToArray(), own.Player.BitVector.ArrayFlags, peer.Player.BitVector.ArrayFlags,
            own.Player.AttackStamp, own.Player.Action, own.Player.HitPoints, own.Player.Mana, own.Player.Stamina, f.Owner.Data.Files());
    }

    private static void AssertFirstUseRefused(FirstUseHitFixture f, FirstUseRefusalState before)
    {
        Assert.Same(before.Actor, f.Actor); Assert.Same(before.Target, f.Victim);
        Assert.Same(before.ActorBinding, f.Actor.NativeGameClient!.OrdinarySpellBinding);
        Assert.Same(before.TargetBinding, f.Victim.NativeGameClient!.OrdinarySpellBinding);
        Assert.Same(before.ProfsOrigin, before.ActorBinding.SavedProficiencies);
        Assert.Same(before.SpellsOrigin, before.TargetBinding.SavedSpells);
        Assert.Same(before.ProficiencyMap, before.ActorBinding.Proficiencies.ClientProf);
        Assert.Empty(before.ActorBinding.Proficiencies.ClientProf);
        Assert.Same(before.ActorSpells, before.ActorBinding.Storage.ClientSpells);
        Assert.Same(before.TargetSpells, before.TargetBinding.Storage.ClientSpells);
        Assert.Same(before.ActorBits, before.ActorBinding.Player.BitVector.bits); Assert.Same(before.TargetBits, before.TargetBinding.Player.BitVector.bits);
        Assert.Equal(before.ActorBitValues, before.ActorBinding.Player.BitVector.bits); Assert.Equal(before.TargetBitValues, before.TargetBinding.Player.BitVector.bits);
        Assert.Same(before.ActorFlagMap, before.ActorBinding.Player.BitVector.ArrayFlags); Assert.Same(before.TargetFlagMap, before.TargetBinding.Player.BitVector.ArrayFlags);
        Assert.Equal(before.Stamp, before.ActorBinding.Player.AttackStamp); Assert.Equal(before.Action, (object)before.ActorBinding.Player.Action);
        Assert.Equal(before.Hp, before.ActorBinding.Player.HitPoints); Assert.Equal(before.Mana, before.ActorBinding.Player.Mana);
        Assert.Equal(before.Stamina, before.ActorBinding.Player.Stamina); Assert.Equal(f.Lethal ? 200 : 2000, before.TargetBinding.Player.HitPoints);
        Assert.False(before.ActorBinding.Client.OnAutoAttack); Assert.False(before.ActorBinding.IsRetired); Assert.False(before.TargetBinding.IsRetired);
        Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(before.ActorBinding)); Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(before.TargetBinding));
        Assert.Empty(f.Owner.Sent); Assert.Empty(f.Owner.Frames(CreationFixture.RuneActor)); Assert.Empty(f.Owner.Frames(DeathTargetAccount));
        Assert.False(f.Owner.Runtime.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
        f.Owner.Data.AssertFiles(before.Files);
    }
}
