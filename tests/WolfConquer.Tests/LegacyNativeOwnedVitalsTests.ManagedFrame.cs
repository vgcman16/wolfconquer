using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeOwnedVitalsTests
{
    [Theory]
    [InlineData(10L, 5000L)] [InlineData(65530L, 100000L)]
    public Task ActualManagedManaFrameAndFreshAdmissionPublishPhysicalManaWithoutUsingTheRefusedComponentSetter(long initial, long maximum) =>
        WorldServerFrameHandlerTests.WithOwnedManagedManaConsumableAsync(3, async f =>
        {
            var expected = checked((uint)(initial + 70));
            await f.Login(); Assert.Equal(checked((uint)initial), f.Player.Mana);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
            var other = Assert.Single(f.Current.PlayerItems.InventoryItems,
                row => row.Uid == WorldServerFrameHandlerTests.ManagedManaConsumableFixture.OtherUid);
            var nativeOther = f.Native.Inventory!.ClientItems[other.Uid];
            var otherBytes = ModeledRow(other);
            var nativeOtherName = nativeOther.OwnerName; var nativeOtherSignature = nativeOther.Signature;
            var nativeOtherLevel = nativeOther.PerfectionLevel; var nativeOtherProgress = nativeOther.PerfectionProgress;
            static byte[] ModeledRow(PlayerItemSnapshotItem row) => PlayerItemSnapshotLoader.ToLegacyBytes(
                PlayerItemSnapshot.Empty with { InventoryItems = new[] { row } });
            void RequireCurrentOther(LegacyNativeGameItemRuntime expectedNative)
            {
                var currentOther = Assert.Single(f.Current.PlayerItems.InventoryItems, row => row.Uid == other.Uid);
                Assert.Equal(otherBytes, ModeledRow(currentOther));
                Assert.Same(expectedNative, f.Native.Inventory!.ClientItems[other.Uid]);
                var currentBinding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
                Assert.Same(f.Current.PlayerItems, currentBinding.ItemSnapshot);
                Assert.Same(f.Current.PlayerItems, currentBinding.Items.Image.FinalSnapshot);
                Assert.True(f.Store.IsBoundSpellOwnerCurrent(currentBinding));
            }
            Exception? observation = null; bool observed = false;
            f.OnSend = sentFrame =>
            {
                if (observed) return; observed = true;
                try
                {
                    Assert.Equal(expected, f.Player.Mana); Assert.Equal((long)expected, f.Current.Character.MinMana);
                    Assert.Equal(10, f.Player.HitPoints); Assert.Equal(10L, f.Current.Character.MinHitPoints);
                    var disk = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _));
                    Assert.Equal((long)expected, disk.MinMana); Assert.Equal(10L, disk.MinHitPoints);
                    Assert.Equal(maximum, disk.MaxMana);
                    Assert.NotSame(other, Assert.Single(f.Current.PlayerItems.InventoryItems, row => row.Uid == other.Uid));
                    RequireCurrentOther(nativeOther);
                    Assert.Same(binding, f.Native.OrdinarySpellBinding); Assert.False(binding.IsRetired);
                    Assert.False(LegacyTradeJournal.HasPending(f.Users)); Assert.False(f.Recovery.IsBlocked);
                    Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                }
                catch (Exception error) { observation = error; throw; }
            };
            int count = f.Sent.Count; await f.Use(); f.OnSend = null;
            if (observation is not null) ExceptionDispatchInfo.Capture(observation).Throw();
            Assert.True(observed); Assert.Equal(2, f.Sent.Count - count);
            Assert.True(LegacyMsgUpdatePacket.TryParse(Frame(f.Sent[^1]), out var update));
            Assert.Equal((ulong)expected, Assert.Single(update.Updates).Value);
            // The prior canonical row is now a detached proposal. Its mutable
            // fields cannot become authority over the new image or later uses.
            var savedItems = File.ReadAllBytes(f.ItemsPath);
            other.OwnerName = "Detached previous owner"; other.Signature = "Detached previous signature";
            other.PerfectionLevel = 17; other.PerfectionProgress = 19;
            RequireCurrentOther(nativeOther); Assert.Equal(savedItems, File.ReadAllBytes(f.ItemsPath));
            Assert.Equal(nativeOtherName, nativeOther.OwnerName); Assert.Equal(nativeOtherSignature, nativeOther.Signature);
            Assert.Equal(nativeOtherLevel, nativeOther.PerfectionLevel); Assert.Equal(nativeOtherProgress, nativeOther.PerfectionProgress);
            await Task.Delay(175); await f.Use(); Assert.Equal(expected + 70U, f.Player.Mana);
            Assert.Equal((long)expected + 70L, f.Current.Character.MinMana); RequireCurrentOther(nativeOther);
            await f.Reload(); Assert.True(binding.IsRetired); Assert.NotSame(binding, f.Native.OrdinarySpellBinding);
            var reloadedOther = f.Native.Inventory!.ClientItems[other.Uid]; Assert.NotSame(nativeOther, reloadedOther);
            RequireCurrentOther(reloadedOther);
            Assert.Equal(expected + 70U, f.Player.Mana); Assert.Equal((long)expected + 70L, f.Current.Character.MinMana);
            await f.Use(); Assert.Equal(expected + 140U, f.Player.Mana); Assert.Equal((long)expected + 140L, f.Current.Character.MinMana);
            RequireCurrentOther(reloadedOther);
            Assert.DoesNotContain("runtime exception", f.Log.ToString(), StringComparison.OrdinalIgnoreCase);
        }, initialMana: initial, maximumMana: maximum);

    [Theory]
    [InlineData(1000L)] [InlineData(65536L)] [InlineData(4294967295L)]
    public Task ActualOwnedManagedRootCastKeepsCostWireOrderAndJointSavedPhysicalManaAtFirstCostOutput(long mana) =>
        LegacyMagicAttackRuntimeTests.RunOwnedRootEquipmentAsync("crop", async f =>
        {
            await f.Query(); await f.Cast(7001); Assert.True(f.HasFlag(0x32)); f.Advance(TimeSpan.FromMilliseconds(500));
            var original = f.Owner;
            var issuer = Assert.IsType<LegacyOrdinarySpellBinding>(original.NativeGameClient!.OrdinarySpellBinding);
            f.Sessions.Set(issuer.Connection, original with { Character = original.Character with { MinMana = mana } });
            var owner = f.Owner; var player = Assert.IsType<LegacyNativePlayerRuntime>(owner.NativePlayer);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(owner.NativeGameClient!.OrdinarySpellBinding);
            var expected = checked((uint)(mana - 40));
            Assert.Equal(checked((uint)mana), player.Mana); Assert.Equal(owner.Character.MinHitPoints, (long)player.HitPoints);
            bool observed = false; var start = f.Order.Count;
            f.OnSend = (_, uid, bytes) =>
            {
                if (observed || uid != owner.Character.Uid || !LegacyMsgUpdatePacket.TryParse(Frame(bytes), out var packet) ||
                    !packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Mana && entry.Value == expected)) return;
                observed = true;
                Assert.Equal(expected, player.Mana); Assert.Equal((long)expected, f.Owner.Character.MinMana);
                Assert.Equal("90", f.Owner.Character.Attributes["Stamina"]);
                var saved = Assert.IsType<CharacterSnapshot>(f.Data.Loader.LoadByAccountId(f.Owner.AccountId));
                Assert.Equal((long)expected, saved.MinMana); Assert.Equal(f.Owner.Character.MinHitPoints, saved.MinHitPoints);
                Assert.Equal(saved.MinHitPoints, (long)player.HitPoints);
                Assert.Same(binding, f.Owner.NativeGameClient!.OrdinarySpellBinding); Assert.False(binding.IsRetired);
                Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
            };
            await f.Cast(1000); f.OnSend = null; Assert.True(observed, f.Output);
            Assert.Equal(new[] { (LegacyMsgUpdateDataType.Stamina, 90UL), (LegacyMsgUpdateDataType.Mana, (ulong)expected) }, f.Costs(start));
            Assert.Equal(expected, player.Mana); Assert.Equal((long)expected, f.Owner.Character.MinMana);
            Assert.DoesNotContain("runtime exception", f.Output, StringComparison.OrdinalIgnoreCase);
        });
}
