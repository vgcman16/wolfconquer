using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlPhysicalLifecycleTests
{
    [Fact]
    public Task ActualAdmissionWithoutTheSelectedLearnedSpellCannotPublishFlashingOrSpendAnything()
    {
        var inputs = LegacyMagicAttackRuntimeTests.OwnedOverlayReentryInputs(false, 17_490, false);
        var actorInput = inputs.Actor with { Spells = PlayerSpellSnapshot.Empty };
        return OwnedEquipmentBehaviorFixture.RunWithNativeGearAsync(actorInput, inputs.World, async f =>
        {
            var actor = f.Current(Actor); var target = f.Current(Target);
            Assert.Empty(actor.PlayerSpells.Spells);
            AssertNoFlashing(actor);
            var native = actor.NativeGameClient!;
            var stamp = Unsafe.BitCast<DateTime, long>(native.Player.AttackStamp);
            var files = f.Data.Files();
            var stamina = actor.Character.Attributes["Stamina"];
            var mana = actor.Character.MinMana;
            f.ActorFrames.Clear(); f.Frames(Target).Clear();
            await f.SendAsync(Actor, Request());
            Assert.Same(actor, f.Current(Actor)); Assert.Same(target, f.Current(Target));
            AssertNoFlashing(f.Current(Actor));
            Assert.False(Flag(f.Current(Actor), FrontBreak));
            Assert.Equal(stamina, f.Current(Actor).Character.Attributes["Stamina"]);
            Assert.Equal(mana, f.Current(Actor).Character.MinMana);
            Assert.Equal(stamp, Unsafe.BitCast<DateTime, long>(native.Player.AttackStamp));
            Assert.False(native.OnAutoAttack); Assert.Null(native.AutoAttack);
            Assert.Empty(f.ActorFrames); Assert.Empty(f.Frames(Target));
            f.Data.AssertFiles(files);
            Assert.False(f.Recovery.IsBlocked);
            Assert.Contains("refused owned Dragonhowl physical", f.Log.ToString());
        }, inputs.Target);
    }
}
