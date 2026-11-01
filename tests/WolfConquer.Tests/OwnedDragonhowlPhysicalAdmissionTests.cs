using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class OwnedDragonhowlPhysicalAdmissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ChangedSavedGearOrConfiguredMetadataRefusesBeforeAnyGameplayMutation(bool configuredMetadata)
    {
        var inputs = LegacyMagicAttackRuntimeTests.OwnedOverlayReentryInputs(false, 17_490, false);
        return OwnedEquipmentBehaviorFixture.RunWithNativeGearAsync(inputs.Actor, inputs.World, async f =>
        {
            const uint actorUid = OwnedEquipmentBehaviorFixture.Actor, targetUid = OwnedEquipmentBehaviorFixture.Peer;
            var actor = f.Current(actorUid); var target = f.Current(targetUid);
            var native = actor.NativeGameClient!; var player = native.Player;
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(native.OrdinarySpellBinding);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.True(f.NativeCombatGear!.IsCurrentFor(inputs.World));
            var gearPath = Path.Combine(f.Data.CombatGear, actorUid.ToString(CultureInfo.InvariantCulture) + ".ini");
            var changedPath = configuredMetadata ? Path.Combine(f.Data.Root,
                "native-physical-proc-metadata", "combat_gear.txt") : gearPath;
            var originalImage = File.ReadAllBytes(changedPath);
            var stamp = Unsafe.BitCast<DateTime, long>(player.AttackStamp);
            var auto = native.AutoAttack; var onAuto = native.OnAutoAttack;
            Assert.False(onAuto); Assert.Null(auto);
            try
            {
                if (configuredMetadata)
                {
                    // A file edit alone does not replace the immutable loaded
                    // tables. Publish a genuine complete later native image.
                    var text = Encoding.UTF8.GetString(originalImage);
                    Assert.Contains("@@201@@", text);
                    File.WriteAllBytes(changedPath, Encoding.UTF8.GetBytes(text.Replace("@@201@@", "@@202@@", StringComparison.Ordinal)));
                    var directory = Path.GetDirectoryName(changedPath)!;
                    Assert.True(LegacyNativeCombatGearRuntime.TryLoad(directory, Path.Combine(directory, "CombatGearRank.txt"),
                        out var replacement, out var reason), reason);
                    Assert.True(Assert.IsType<LegacyNativeCombatGearRuntime>(replacement).IsCurrent);
                    Assert.False(f.NativeCombatGear.IsCurrent);
                }
                else
                {
                    var saved = new CombatGearSnapshotLoader(f.Data.CombatGear).LoadByCharacterUid(actorUid);
                    Assert.Contains(saved.Items, row => row.ItemID == 201 && row.Equip == 1);
                    var changed = saved with { Items = saved.Items.Select(row => row with { Equip = 0 }).ToArray() };
                    new CombatGearSnapshotLoader(f.Data.CombatGear).SaveByCharacterUid(actorUid, changed);
                    Assert.DoesNotContain(new CombatGearSnapshotLoader(f.Data.CombatGear).LoadByCharacterUid(actorUid).Items,
                        row => row.Equip == 1);
                }
                Assert.False(originalImage.AsSpan().SequenceEqual(File.ReadAllBytes(changedPath)));
                var files = f.Data.Files();
                f.ActorFrames.Clear(); f.Frames(targetUid).Clear();
                await f.SendAsync(actorUid, new MsgAttackPacket(new MsgAttackQuery(Uid: actorUid,
                    OpponentUid: targetUid, X: 831, Y: 584, AttackType: MsgAttackType.Physical,
                    Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes());
                Assert.Same(actor, f.Current(actorUid)); Assert.Same(target, f.Current(targetUid));
                Assert.Equal(stamp, Unsafe.BitCast<DateTime, long>(player.AttackStamp));
                Assert.Equal(onAuto, native.OnAutoAttack); Assert.Same(auto, native.AutoAttack);
                Assert.Empty(f.ActorFrames); Assert.Empty(f.Frames(targetUid));
                Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
                f.Data.AssertFiles(files);
                Assert.Contains("refused owned Dragonhowl physical", f.Log.ToString());
                Assert.DoesNotContain("flashingNamePrefix", f.Log.ToString());
            }
            finally { File.WriteAllBytes(changedPath, originalImage); }
        }, inputs.Target);
    }
}
