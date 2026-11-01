using System.Runtime.CompilerServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    private const uint TeleportTimestamp = 4321;
    private sealed record TeleportBefore(Dictionary<string, byte[]> Files, WorldSession ActorSession, WorldSession TargetSession,
        LegacyOrdinarySpellBinding ActorBinding, LegacyOrdinarySpellBinding TargetBinding,
        LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot Native, LegacyNativeInteractQuery Descriptor,
        MsgAttackQuery DescriptorValues, long StampBits, object ActorItems, object TargetItems,
        object ActorSpells, object TargetSpells, object ActorProfs, object TargetProfs, ushort Px, ushort Py);

    private static TeleportBefore CaptureTeleportBefore(AttackFixture f)
    {
        var actor = f.Current(); var target = f.Current(Target); var own = f.Binding(); var peer = f.Binding(Target);
        Assert.True(own.Client.OnAutoAttack); Assert.NotNull(own.Client.AutoAttack);
        // Read the exact already-issued historical snapshot. It cannot issue
        // a change, movement/teleport purpose or permission from test DATA.
        Assert.True(f.Owner.Handler.TryGetBasicPhysicalRetirementSnapshot(own, out var snapshot));
        var actual = Assert.IsType<LegacyNativeGameClientRuntime.BasicPhysicalNativeSnapshot>(snapshot);
        Assert.True(actual.OnAutoAttack); Assert.NotNull(actual.Request);
        return new(f.Files(), actor, target, own, peer, actual, own.Client.AutoAttack!,
            ReadTeleportDescriptor(own.Client.AutoAttack!), Unsafe.BitCast<DateTime, long>(own.Player.AttackStamp),
            own.Items.Image, peer.Items.Image, own.Storage.ClientSpells, peer.Storage.ClientSpells,
            own.Proficiencies.ClientProf, peer.Proficiencies.ClientProf, own.Player.Px, own.Player.Py);
    }

    private static async Task<WorldSession?> ExecuteOwnedDestinationAsync(AttackFixture f, LegacyTeleportDestination destination)
    {
        var current = f.Current();
        // Existing actual Frame context factory, not a fabricated teleport
        // owner/terrain/request. This tests the already-reduced phase only.
        var context = f.Owner.Handler.CreateMonsterRuntimeContext(new(f.Owner.Connection(Actor), current));
        Assert.Same(f.Owner.Store, context.Sessions); Assert.Same(f.Clock, context.TimeProvider);
        Assert.Null(context.TeleportOwner); Assert.Null(context.TeleportTerrain);
        return await WorldPacketHandlers.ExecuteLegacyTeleportDestinationAsync(context, current, destination,
            TeleportTimestamp, CancellationToken.None);
    }

    private static MsgAttackQuery ReadTeleportDescriptor(LegacyNativeInteractQuery query) => new(query.UID, query.OpponentUID,
        query.X, query.Y, (MsgAttackType)query.AtkType, query.Damage, query.KilledMonster, query.ResponseDamage,
        (MsgAttackEffect)query.Effect, query.Unknown, query.CombatGear, query.SpellLevel, query.SpellID);

    private static async Task AssertTeleportRepeatStaysCancelledAsync(AttackFixture f, TeleportBefore before,
        Dictionary<string, byte[]> expected, LegacyTeleportDestination destination, long targetHp)
    {
        int count = f.Order.Count; f.Clock.Advance(OrdinaryDelay * 5); await f.TickAsync();
        Assert.Equal(count, f.Order.Count);
        AssertTeleportCommitted(f, before, expected, destination, targetHp);
    }
}
