using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player455/979/989/991, actual physical state not capability snapshots.
    public bool AttackHit = false;
    public uint ShieldBlockDamage = 0u;
    private long attackStampBits;
    public DateTime AttackStamp
    {
        get => Unsafe.BitCast<long, DateTime>(Volatile.Read(ref attackStampBits));
        set
        {
            if (Owner.HasOwnedBasicPhysicalAttachment || HasOwnedActionAttachment)
                throw new InvalidOperationException("ordinaryAttackStampRequiresOwnedPlan");
            if (Monitor.IsEntered(SpellAdmissionSyncRoot) && !Monitor.IsEntered(Owner.SpellAdmissionSyncRoot))
                throw new InvalidOperationException("nativeAttackStampActorOrderInvalid");
            lock (Owner.SpellAdmissionSyncRoot)
            lock (SpellAdmissionSyncRoot)
            {
                if (Owner.HasOwnedBasicPhysicalAttachment || HasOwnedActionAttachment)
                    throw new InvalidOperationException("ordinaryAttackStampRequiresOwnedPlan");
                Volatile.Write(ref attackStampBits, Unsafe.BitCast<DateTime, long>(value));
            }
        }
    }
    public DateTime SpellAttackStamp = default(DateTime);
}

public sealed partial class LegacyNativeGameClientRuntime
{
    // Native GameClient233/235. Ordinary writes use the actual typed publisher.
    private LegacyNativeInteractQuery? autoAttack;
    private bool onAutoAttack;
    internal bool HasOwnedBasicPhysicalAttachment => OrdinarySpellBinding is not null ||
        Player?.HasOwnedActionAttachment == true || Player?.HasOwnedVitalsAttachment == true;
    internal LegacyNativeInteractQuery? AutoAttack
    {
        get => Volatile.Read(ref autoAttack);
        set
        {
            if (HasOwnedBasicPhysicalAttachment) throw new InvalidOperationException("ordinaryAutoAttackRequiresOwnedPlan");
            var player = Player ?? throw new InvalidOperationException("nativeAutoAttackPlayerMissing");
            if (Monitor.IsEntered(player.SpellAdmissionSyncRoot) && !Monitor.IsEntered(SpellAdmissionSyncRoot))
                throw new InvalidOperationException("nativeAutoAttackActorOrderInvalid");
            lock (SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
            {
                if (HasOwnedBasicPhysicalAttachment || !ReferenceEquals(Player, player) || !ReferenceEquals(player.Owner, this))
                    throw new InvalidOperationException("ordinaryAutoAttackRequiresOwnedPlan");
                Volatile.Write(ref autoAttack, value);
            }
        }
    }
    public bool OnAutoAttack
    {
        get => Volatile.Read(ref onAutoAttack);
        set
        {
            if (HasOwnedBasicPhysicalAttachment) throw new InvalidOperationException("ordinaryAutoAttackRequiresOwnedPlan");
            var player = Player ?? throw new InvalidOperationException("nativeAutoAttackPlayerMissing");
            if (Monitor.IsEntered(player.SpellAdmissionSyncRoot) && !Monitor.IsEntered(SpellAdmissionSyncRoot))
                throw new InvalidOperationException("nativeAutoAttackActorOrderInvalid");
            lock (SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
            {
                if (HasOwnedBasicPhysicalAttachment || !ReferenceEquals(Player, player) || !ReferenceEquals(player.Owner, this))
                    throw new InvalidOperationException("ordinaryAutoAttackRequiresOwnedPlan");
                Volatile.Write(ref onAutoAttack, value);
            }
        }
    }
}
