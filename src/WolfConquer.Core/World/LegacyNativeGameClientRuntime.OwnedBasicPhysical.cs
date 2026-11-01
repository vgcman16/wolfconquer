using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    internal enum BasicPhysicalNativeKind { CommittedHit, ArmRepeat, CancelRepeat }

    // Empty/unregistered objects confer nothing. Actual origins/data stay in
    // this private table; a proposed no-op is not a committed live snapshot.
    internal sealed class BasicPhysicalNativeSnapshot
    {
        internal bool OnAutoAttack => RequireBasicSnapshot(this).On;
        internal MsgAttackQuery? Request => RequireBasicSnapshot(this).Request;
        internal DateTime AttackStamp => Unsafe.BitCast<long, DateTime>(RequireBasicSnapshot(this).StampBits);
        internal static BasicPhysicalNativeSnapshot Capture(LegacyOrdinarySpellBinding binding)
        {
            ArgumentNullException.ThrowIfNull(binding);
            if (!BasicNativeOwnerCurrentPlain(binding)) throw InvalidBasicNative();
            var client = binding.Client;
            var descriptor = Volatile.Read(ref client.autoAttack);
            var captured = RegisterBasicSnapshot(new(binding, Volatile.Read(ref client.onAutoAttack), descriptor,
                ReadBasicDescriptor(descriptor), Unsafe.BitCast<DateTime, long>(binding.Player.AttackStamp), false, true));
            if (!captured.IsCurrentPlain(binding)) throw InvalidBasicNative();
            return captured;
        }
        internal bool IsCurrentPlain(LegacyOrdinarySpellBinding binding) =>
            BasicSnapshots.TryGetValue(this, out var data) && data.Committed && !data.Retirement &&
            ReferenceEquals(data.Binding, binding) && BasicNativeOwnerCurrentPlain(binding) && BasicTupleCurrent(data);
        internal bool IsCurrentForRetirementPlain(LegacyOrdinarySpellBinding binding) =>
            BasicSnapshots.TryGetValue(this, out var data) && data.Committed && ReferenceEquals(data.Binding, binding) &&
            BasicNativeRetirementActorsCurrent(binding) && binding.Store.IsBasicPhysicalRetirementOwnerCurrent(binding, this) && BasicTupleCurrent(data);
    }

    private sealed class BasicSnapshotData(LegacyOrdinarySpellBinding binding, bool on,
        LegacyNativeInteractQuery? descriptor, MsgAttackQuery? request, long stampBits, bool retirement, bool committed)
    {
        internal readonly LegacyOrdinarySpellBinding Binding = binding;
        internal readonly LegacyNativeGameClientRuntime Client = binding.Client;
        internal readonly LegacyNativePlayerRuntime Player = binding.Player;
        internal readonly bool On = on, Retirement = retirement;
        internal readonly LegacyNativeInteractQuery? Descriptor = descriptor;
        internal readonly MsgAttackQuery? Request = request;
        internal readonly long StampBits = stampBits;
        internal bool Committed = committed; // Preallocated; set only at actual native publication.
    }
    private static readonly ConditionalWeakTable<BasicPhysicalNativeSnapshot, BasicSnapshotData> BasicSnapshots = new();

    private static BasicPhysicalNativeSnapshot RegisterBasicSnapshot(BasicSnapshotData data)
    {
        var snapshot = new BasicPhysicalNativeSnapshot(); BasicSnapshots.Add(snapshot, data); return snapshot;
    }
    private static BasicSnapshotData RequireBasicSnapshot(BasicPhysicalNativeSnapshot snapshot) =>
        BasicSnapshots.TryGetValue(snapshot, out var data) ? data : throw InvalidBasicNative();

    private static bool BasicNativeActorsCurrent(LegacyOrdinarySpellBinding binding) =>
        Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) &&
        !binding.IsRetired && ReferenceEquals(binding.Connection.NativeGameClient, binding.Client) &&
        ReferenceEquals(binding.Client.OrdinarySpellBinding, binding) && ReferenceEquals(binding.Client.Player, binding.Player) &&
        ReferenceEquals(binding.Player.Owner, binding.Client) && binding.Player.UID == binding.Uid;

    private static bool BasicNativeOwnerCurrentPlain(LegacyOrdinarySpellBinding binding) => BasicNativeActorsCurrent(binding) &&
        binding.Store.TryGet(binding.Connection, out var current) && current is not null &&
        binding.Store.IsSpellFileWriteOwnerCurrent(binding.Connection, current, binding.NativeIdentity, binding);

    private static bool BasicNativeRetirementActorsCurrent(LegacyOrdinarySpellBinding binding) =>
        Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) && Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) &&
        ReferenceEquals(binding.Connection.NativeGameClient, binding.Client) &&
        ReferenceEquals(binding.Client.OrdinarySpellBinding, binding) && ReferenceEquals(binding.Client.Player, binding.Player) &&
        ReferenceEquals(binding.Player.Owner, binding.Client) && binding.Player.UID == binding.Uid;

    private static bool BasicTupleCurrent(BasicSnapshotData data) =>
        ReferenceEquals(data.Client, data.Binding.Client) && ReferenceEquals(data.Player, data.Binding.Player) &&
        Volatile.Read(ref data.Client.onAutoAttack) == data.On &&
        ReferenceEquals(Volatile.Read(ref data.Client.autoAttack), data.Descriptor) && BasicDescriptorCurrent(data) &&
        Unsafe.BitCast<DateTime, long>(data.Player.AttackStamp) == data.StampBits;

    private static bool BasicDescriptorCurrent(BasicSnapshotData data) =>
        (data.Descriptor is null || data.Descriptor.GetType() == typeof(LegacyNativeInteractQuery)) &&
        ReadBasicDescriptor(data.Descriptor) == data.Request;

    private static MsgAttackQuery? ReadBasicDescriptor(LegacyNativeInteractQuery? descriptor)
    {
        if (descriptor is null) return null;
        if (descriptor.GetType() != typeof(LegacyNativeInteractQuery)) throw InvalidBasicNative();
        return new(descriptor.UID, descriptor.OpponentUID, descriptor.X, descriptor.Y, (MsgAttackType)descriptor.AtkType,
            descriptor.Damage, descriptor.KilledMonster, descriptor.ResponseDamage, (MsgAttackEffect)descriptor.Effect,
            descriptor.Unknown, descriptor.CombatGear, descriptor.SpellLevel, descriptor.SpellID);
    }

    private static InvalidOperationException InvalidBasicNative() => new("ordinaryBasicPhysicalNativeOwnerChanged");
}
