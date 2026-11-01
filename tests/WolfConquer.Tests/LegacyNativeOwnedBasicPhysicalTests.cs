using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeOwnedBasicPhysicalTests
{
    [Theory]
    [InlineData((int)DateTimeKind.Unspecified)]
    [InlineData((int)DateTimeKind.Utc)]
    public void WholeUnboundComponentKeepsNumericSettersAndExactStampBits(int kind)
    {
        // Unbound DATA/component state, never admission or an accepted attack.
        var client = new LegacyNativeGameClientRuntime(); var descriptor = new LegacyNativeInteractQuery { Damage = 17, Unknown = 19 };
        var stamp = new DateTime(2026, 10, 31, 12, 0, 0, (DateTimeKind)kind);
        client.Player.AttackStamp = stamp; client.AutoAttack = descriptor; client.OnAutoAttack = true;
        Assert.Equal(Unsafe.BitCast<DateTime, long>(stamp), Unsafe.BitCast<DateTime, long>(client.Player.AttackStamp));
        Assert.Same(descriptor, client.AutoAttack); Assert.True(client.OnAutoAttack); Assert.Null(client.OrdinarySpellBinding);
        Assert.Throws<ArgumentNullException>(() => LegacyNativeMsgAttackPacket.CreateAutoAtack(null!, client));
        Assert.Throws<ArgumentNullException>(() => LegacyNativeMsgAttackPacket.CreateAutoAtack(descriptor, null!));
        Assert.Same(descriptor, client.AutoAttack); Assert.True(client.OnAutoAttack);
        Assert.Equal(Unsafe.BitCast<DateTime, long>(stamp), Unsafe.BitCast<DateTime, long>(client.Player.AttackStamp));
        client.OnAutoAttack = false; Assert.Same(descriptor, client.AutoAttack); Assert.False(client.OnAutoAttack);
    }

    [Fact]
    public Task ActualAttachedAndEofRetiredOwnersRejectEveryDirectWriterAndWholeNativeArmingBeforeEffects()
    {
        var actor = new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(minHitPoints: 1), PlayerItemSnapshot.Empty);
        return OwnedEquipmentBehaviorFixture.RunAsync(actor, OwnedEquipmentBehaviorFixture.Catalog(), async f =>
        {
            f.ActorFrames.Clear(); var current = f.Current(); var client = current.NativeGameClient!; var player = client.Player;
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(client.OrdinarySpellBinding);
            var files = f.Data.Files(); var stamp = Unsafe.BitCast<DateTime, long>(player.AttackStamp);
            bool on = client.OnAutoAttack; var descriptor = client.AutoAttack;
            LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot? captured = null;
            Assert.True(f.Store.WithCurrentNativeClientIdentity(f.Connection(OwnedEquipmentBehaviorFixture.Actor), current.Character.Uid, identity =>
            {
                Assert.Same(binding.NativeIdentity, identity);
                lock (client.SpellAdmissionSyncRoot)
                lock (player.SpellAdmissionSyncRoot)
                {
                    captured = LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot.Capture(binding);
                    Assert.True(captured.IsCurrentPlain(binding));
                    Assert.False(new LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot().IsCurrentPlain(binding));
                }
            }));
            var snapshot = Assert.IsType<LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot>(captured);
            Assert.Equal(on, snapshot.OnAutoAttack); Assert.Equal(stamp, Unsafe.BitCast<DateTime, long>(snapshot.AttackStamp));
            Assert.False(on); Assert.Null(descriptor); Assert.Null(snapshot.Request);
            Assert.Throws<InvalidOperationException>(() => client.OnAutoAttack = true);
            Assert.Throws<InvalidOperationException>(() => client.OnAutoAttack = false);
            Assert.Throws<InvalidOperationException>(() => client.AutoAttack = new LegacyNativeInteractQuery());
            Assert.Throws<InvalidOperationException>(() => player.AttackStamp = DateTime.MinValue);
            LegacyNativeMsgAttackPacket.CreateAutoAtack(new() { UID = 7, OpponentUID = 8, AtkType = 2 }, client);
            Assert.Throws<ArgumentNullException>(() => LegacyNativeMsgAttackPacket.CreateAutoAtack(null!, client));
            Assert.Equal(on, client.OnAutoAttack); Assert.Same(descriptor, client.AutoAttack);
            Assert.Equal(stamp, Unsafe.BitCast<DateTime, long>(player.AttackStamp));
            Assert.Same(current, f.Current()); f.Data.AssertFiles(files); Assert.Empty(f.ActorFrames);

            await f.Connection(OwnedEquipmentBehaviorFixture.Actor).CompleteTransportCloseAsync();
            Assert.False(f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out _)); Assert.True(binding.IsRetired);
            Assert.Same(binding, client.OrdinarySpellBinding);
            Assert.Throws<InvalidOperationException>(() => client.OnAutoAttack = true);
            Assert.Throws<InvalidOperationException>(() => client.OnAutoAttack = false);
            Assert.Throws<InvalidOperationException>(() => client.AutoAttack = null);
            Assert.Throws<InvalidOperationException>(() => player.AttackStamp = DateTime.UtcNow);
            LegacyNativeMsgAttackPacket.CreateAutoAtack(new() { UID = 7, AtkType = 2 }, client);
            Assert.Equal(on, client.OnAutoAttack); Assert.Same(descriptor, client.AutoAttack);
            Assert.Equal(stamp, Unsafe.BitCast<DateTime, long>(player.AttackStamp));
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
                Assert.False(snapshot.IsCurrentPlain(binding));
            f.Data.AssertFiles(files); Assert.Empty(f.ActorFrames);
        });
    }
}
