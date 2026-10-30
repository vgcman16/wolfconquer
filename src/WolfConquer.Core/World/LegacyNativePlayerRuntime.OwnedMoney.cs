namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    private LegacyOrdinarySpellBinding? ownedMoneyBinding;
    private LegacyOrdinarySpellBinding? AttachedMoneyBinding => Volatile.Read(ref ownedMoneyBinding) ??
        Owner.OrdinarySpellBinding ?? Owner.MySpells?.OrdinaryBinding;
    internal bool HasOwnedMoneyAttachment => AttachedMoneyBinding is not null;

    private void SetCurrentMoney(long value)
    {
        if (AttachedMoneyBinding is not null)
            throw new InvalidOperationException("ordinaryMoneyRequiresOwnedStorePlan");
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Money cannot be negative.");
        if (Monitor.IsEntered(SpellAdmissionSyncRoot) && !Monitor.IsEntered(Owner.SpellAdmissionSyncRoot))
            throw new InvalidOperationException("nativeMoneyActorOrderInvalid");
        lock (Owner.SpellAdmissionSyncRoot)
        lock (SpellAdmissionSyncRoot)
        {
            if (AttachedMoneyBinding is not null)
                throw new InvalidOperationException("ordinaryMoneyRequiresOwnedStorePlan");
            WriteUnboundMoney(value);
        }
    }

    // Like the joint vital participant, this capability belongs only to the
    // real Store publication. It has no saver, sender or accepting callback.
    internal sealed class OwnedMoneyPublication
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyOrdinarySpellBinding? beforeBinding;
        private readonly long before, after;

        private OwnedMoneyPublication(LegacyOrdinarySpellBinding binding, long after)
        {
            this.binding = binding; player = binding.Player;
            before = Volatile.Read(ref player._Money); this.after = after;
            if (before < 0 || after < 0) throw new InvalidOperationException("ordinaryMoneyBalanceInvalid");
            beforeBinding = Volatile.Read(ref player.ownedMoneyBinding);
        }

        internal static OwnedMoneyPublication PrepareAdmission(LegacyOrdinarySpellBinding binding,
            LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner owner, WorldSession proposed)
        {
            if (!WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                !owner.BeforeOwnerIsCurrentPlain(binding.Client, binding.Player) ||
                owner.PreparedUid != proposed.Character.Uid || binding.Player.ownedMoneyBinding is not null)
                throw new InvalidOperationException("ordinaryMoneyAdmissionOwnerInvalid");
            return new(binding, proposed.Character.Money);
        }

        internal static OwnedMoneyPublication PrepareBound(LegacyOrdinarySpellBinding binding,
            WorldSession expected, WorldSession proposed)
        {
            if (!WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                !binding.Store.IsBoundSpellOwnerCurrent(binding) || !binding.Store.TryGet(binding.Connection, out var current) ||
                !ReferenceEquals(current, expected) || !ReferenceEquals(binding.Player.ownedMoneyBinding, binding) ||
                expected.Character.Money != Volatile.Read(ref binding.Player._Money))
                throw new InvalidOperationException("ordinaryMoneyPhysicalBaselineMismatch");
            return new(binding, proposed.Character.Money);
        }

        internal bool IsCurrentPlain() => Volatile.Read(ref player._Money) == before &&
            ReferenceEquals(Volatile.Read(ref player.ownedMoneyBinding), beforeBinding);

        internal void PublishPrepared()
        {
            Volatile.Write(ref player._Money, after);
            Volatile.Write(ref player.ownedMoneyBinding, binding);
        }
    }
}
