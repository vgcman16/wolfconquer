using System.Buffers.Binary;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    private const ushort TransitionFineRain = 14_500, TransitionDragonhowl = 17_490;
    private const uint TransitionRight = 1_083_200, TransitionLeft = 1_083_201;
    private const uint TransitionBaseMaximum = 447 * 24 + 10 * 3 + 2 * 3;
    private const long TransitionBuffedHp = TransitionBaseMaximum + 100;

    private static Task RunChangedWindWalkerDeath(bool proc, Func<AttackFixture, ILegacySignedMillisecondClock, Task> body)
    {
        // These are the same retained valid class16000 fan rows as the existing
        // genuine WindWalker/death controls. No Warrior receives fan equipment.
        string[] raw = [
            "626003@@PrideFan@@16000@@0@@5@@0@@0@@2@@0@@0@@0@@153@@0@@9@@5@@0@@51@@0@@0@@0@@1599@@1599@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@1000@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@Handfan@@None@@5@@0@@0@@0@@1340@@0@@0@@0@@0@@0@@0@@",
            "626009@@PrideFan@@16000@@0@@5@@0@@0@@2@@0@@0@@0@@153@@0@@14@@9@@0@@63@@0@@0@@0@@1599@@1599@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@1000@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@10@@0@@0@@0@@0@@0@@0@@0@@Handfan@@None@@5@@0@@0@@0@@1340@@0@@0@@0@@0@@0@@0@@",
        ];
        var fans = raw.Select(line => line.Split("@@", StringSplitOptions.None))
            .Select(fields => new ItemType(int.Parse(fields[0], CultureInfo.InvariantCulture), fields[1], fields)).ToArray();
        Assert.All(fans, row =>
        {
            Assert.Equal(0U, row.ItemHP); Assert.Equal(16_000U, row.RequiredClass);
            Assert.Equal(0U, row.RequiredStrength); Assert.Equal(2U, row.RequiredAgility);
        });
        // Reuse declared DATA rows from the passing owned equipment scenarios:
        //14500 raw0 normalizes to100;17490 is the actual Physical proc selection.
        var fineRainData = LegacyMagicAttackRuntimeTests.CreateOwnedPhysicalEquipmentInputs("distinct", Actor, Target);
        var procData = LegacyMagicAttackRuntimeTests.OwnedOverlayReentryInputs(false, TransitionDragonhowl, false);
        var magic = new[] { Assert.Single(fineRainData.World.Magic, row => row.Id == TransitionFineRain),
            Assert.Single(procData.World.Magic, row => row.Id == TransitionDragonhowl) };
        var catalog = NativeCallerWorldDataFixture.Create(fans, [], magic: magic,
            combatGear: proc ? NativeCombatGearProcFixture.CatalogRows() : Array.Empty<CombatGear>());
        var actor = Saved(Actor, "ChangedFanKiller", 830, 5000, 15_000, false) with { Class = proc ? 2_005 : 7_051 };
        if (proc)
        {
            var attributes = new Dictionary<string, string>(actor.Attributes, StringComparer.OrdinalIgnoreCase) { ["DynamicID"] = "0" };
            foreach (var key in new[] { "LegacyCombatGearAttackSpellIndex", "LegacyCombatGearAttackRateRoll" })
                attributes[key] = procData.Actor.Character.Attributes[key];
            actor = actor with { Attributes = attributes };
        }
        var target = Saved(Target, "ChangedFanTarget", 831, 200, 0, false) with { Class = 16_051, Agility = 2 };
        if (proc)
            target = target with { Attributes = new Dictionary<string, string>(target.Attributes,
                StringComparer.OrdinalIgnoreCase) { ["DynamicID"] = "0" } };
        // A plain-map victim legitimately earns the proc flashing prefix used
        // below to observe whole-proc preflight before the actual death writer.
        var targetItems = new PlayerItemSnapshot([], [], [],
            new[] { DeathFanItem(TransitionRight, 626_003, 4), DeathFanItem(TransitionLeft, 626_009, 5) }, []);
        var actorInput = new OwnedEquipmentBehaviorInput(actor, PlayerItemSnapshot.Empty,
            proc ? procData.Actor.Spells : PlayerSpellSnapshot.Empty, CombatGear: proc ? procData.Actor.CombatGear : null);
        var targetInput = new OwnedEquipmentBehaviorInput(target, targetItems,
            new PlayerSpellSnapshot(new PlayerSpellSnapshotEntry[] { new(TransitionFineRain, 0, 0, 0, 0, 0) }));
        var clock = new AttackClock();
        var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
            LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000));
        Task Exercise(OwnedEquipmentBehaviorFixture owner) => body(new AttackFixture(owner, clock), options.LegacyClock!);
        return proc
            ? OwnedEquipmentBehaviorFixture.RunWithNativeGearOptionsAsync(actorInput, catalog, options, Exercise, targetInput)
            : OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(actorInput, catalog, options, Exercise, targetInput);
    }

    private static async Task<LegacyNativeStatusFlagsBigVector32.Flag> DeferRealFanRemovalAsync(AttackFixture f,
        ILegacySignedMillisecondClock clock)
    {
        var original = Assert.Single(f.Binding(Target).Player.BitVector.GetFlags(), row => row.Key == 194);
        Assert.False(original.RemoveOnDead);
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(f.Current(Target).Character.Attributes, 194));
        Assert.Equal(16_051L, f.Current(Target).Character.Class);
        Assert.Equal(2L, f.Current(Target).Character.Agility);
        Assert.Equal(200L, f.Current(Target).Character.MinHitPoints);
        Assert.Equal(new ushort[] { 4, 5 }, f.Current(Target).PlayerItems.EquipmentItems.Select(row => row.Position).Order());
        f.Clear();
        await f.Owner.SendAsync(Target, new MsgAttackPacket(new(Target, Target, 831, 584, MsgAttackType.Magic,
            0, 0, 0, MsgAttackEffect.None, 0, SpellId: TransitionFineRain)).ToFrameBytes());
        var buffed = f.Current(Target);
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(buffed.Character.Attributes, 212), f.Owner.Log.ToString());
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(buffed.Character.Attributes, 213));
        Assert.Equal("100", buffed.Character.Attributes["FineRainPower"]);
        Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(buffed).StampMilliseconds);
        Assert.Equal(TransitionBuffedHp, buffed.Character.MinHitPoints);
        Assert.Equal((int)TransitionBuffedHp, f.Binding(Target).Player.HitPoints);
        Assert.Same(original, f.Binding(Target).Player.BitVector.ArrayFlags[194]);
        Assert.Contains(f.Order, row => LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(row.Bytes),
            out var animation) && animation.SpellId == TransitionFineRain);
        f.Clear();

        // The durable move keeps the exact physical preimage row order; only
        // the left fan's position changes. Logical snapshots group inventory first.
        var itemBefore = File.ReadAllBytes(f.Owner.ItemPath(Target));
        Assert.Equal(4 + 2 * 184 + 4, itemBefore.Length);
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(itemBefore));
        Assert.Equal(TransitionRight, BinaryPrimitives.ReadUInt32LittleEndian(itemBefore.AsSpan(4)));
        Assert.Equal(TransitionLeft, BinaryPrimitives.ReadUInt32LittleEndian(itemBefore.AsSpan(4 + 184)));
        Assert.Equal((ushort)4, BinaryPrimitives.ReadUInt16LittleEndian(itemBefore.AsSpan(4 + 12)));
        Assert.Equal((ushort)5, BinaryPrimitives.ReadUInt16LittleEndian(itemBefore.AsSpan(4 + 184 + 12)));
        var expectedSavedItems = itemBefore.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(expectedSavedItems.AsSpan(4 + 184 + 12), 0);
        await f.Owner.DispatchAsync(new(TransitionLeft, 5, MsgItemUsageAction.Unequip, 1235, 0, 0, 0), Target);
        Assert.Contains(f.Owner.Frames(Target), bytes => bytes.AsSpan().SequenceEqual(
            LegacyMsgMessagePacket.SystemTalk("You Shoud wait 6 seconds.").ToFrameBytes()));
        var deferred = f.Current(Target);
        Assert.Equal(626_003U, Assert.Single(deferred.PlayerItems.EquipmentItems).ItemId);
        var removed = Assert.Single(deferred.PlayerItems.InventoryItems, item => item.Uid == TransitionLeft);
        Assert.Equal((ushort)0, removed.Position); Assert.Equal(626_009U, removed.ItemId);
        Assert.Same(original, f.Binding(Target).Player.BitVector.ArrayFlags[194]);
        Assert.True(LegacyPlayerDeathState.HasStatusFlag(deferred.Character.Attributes, 194));
        Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(deferred).StampMilliseconds);
        Assert.Equal(TransitionBuffedHp, deferred.Character.MinHitPoints);
        var savedItems = File.ReadAllBytes(f.Owner.ItemPath(Target));
        Assert.Equal(expectedSavedItems, savedItems);
        var logicalSavedItems = PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(savedItems));
        Assert.Equal(logicalSavedItems, PlayerItemSnapshotLoader.ToLegacyBytes(deferred.PlayerItems));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(deferred.PlayerItems),
            PlayerItemSnapshotLoader.ToLegacyBytes(f.Binding(Target).Items.Image.FinalSnapshot));
        Assert.True(LegacyNativeMagicQueryClient.TryRead(f.Owner.Store, f.Owner.Connection(Target), Target, out var retained,
            f.Binding(Target).NativeIdentity));
        Assert.Equal(626_009U, retained.LeftWeaponItemId); // Real deferred native cache, not a forged old row.
        f.Clock.Advance(6_001);
        Assert.Equal(16_001, clock.GetMilliseconds());
        Assert.True(LegacyEquipmentRebuildAdmission.ApplyCurrent(deferred, LegacyEquipmentRebuildReason.PlayerDeath,
            clock).Admitted);
        f.Clear();
        return original;
    }
}
