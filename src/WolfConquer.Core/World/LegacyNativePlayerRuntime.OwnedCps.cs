using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    private LegacyOrdinarySpellBinding? ownedCpsBinding;
    private LegacyOrdinarySpellBinding? AttachedCpsBinding => Volatile.Read(ref ownedCpsBinding) ??
        Owner.OrdinarySpellBinding ?? Owner.MySpells?.OrdinaryBinding;
    internal bool HasOwnedCpsAttachment => AttachedCpsBinding is not null;

    private void RequireUnboundCpsSetter()
    {
        if (AttachedCpsBinding is not null)
            throw new InvalidOperationException("ordinaryCpsRequiresOwnedStorePlan");
    }

    private void RequireCpsActorOrder()
    {
        if (Monitor.IsEntered(SpellAdmissionSyncRoot) && !Monitor.IsEntered(Owner.SpellAdmissionSyncRoot))
            throw new InvalidOperationException("nativeCpsActorOrderInvalid");
    }

    // One closed participant over the three actual native slots. Current
    // physical widths are prerequisites, not inferred wire/gameplay limits.
    internal sealed class OwnedCpsPublication
    {
        private readonly LegacyOrdinarySpellBinding binding;
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyOrdinarySpellBinding? beforeBinding;
        private readonly uint beforeCps, afterCps;
        private readonly int beforeBound, afterBound;
        private readonly ulong beforeBank, afterBank;

        private OwnedCpsPublication(LegacyOrdinarySpellBinding binding, CharacterSnapshot proposed)
        {
            this.binding = binding; player = binding.Player; client = binding.Client;
            RequireRepresentable(proposed);
            beforeCps = Volatile.Read(ref player._cps);
            beforeBound = Volatile.Read(ref player._bountCps);
            beforeBank = Volatile.Read(ref client.ConquerPointsSave);
            beforeBinding = Volatile.Read(ref player.ownedCpsBinding);
            if (beforeBound < 0) throw new InvalidOperationException("ordinaryCpsPhysicalBaselineInvalid");
            afterCps = checked((uint)proposed.ConquerPoints);
            afterBound = checked((int)proposed.BoundConquerPoints);
            afterBank = checked((ulong)proposed.BankConquerPoints);
        }

        internal static void RequireRepresentable(CharacterSnapshot proposed)
        {
            ArgumentNullException.ThrowIfNull(proposed);
            if (proposed.ConquerPoints is < 0 or > uint.MaxValue ||
                proposed.BoundConquerPoints is < 0 or > int.MaxValue || proposed.BankConquerPoints < 0)
                throw new InvalidOperationException("ordinaryCpsNativeRepresentationUnavailable");
        }

        internal static OwnedCpsPublication PrepareAdmission(LegacyOrdinarySpellBinding binding,
            LegacyNativeExistingCharacterPrefix.SpellAdmissionOwner owner, WorldSession proposed)
        {
            if (!WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                !owner.BeforeOwnerIsCurrentPlain(binding.Client, binding.Player) ||
                owner.PreparedUid != proposed.Character.Uid || binding.Player.ownedCpsBinding is not null)
                throw new InvalidOperationException("ordinaryCpsAdmissionOwnerInvalid");
            return new(binding, proposed.Character);
        }

        internal static OwnedCpsPublication PrepareBound(LegacyOrdinarySpellBinding binding,
            WorldSession expected, WorldSession proposed)
        {
            RequireRepresentable(expected.Character);
            if (!WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate ||
                !Monitor.IsEntered(binding.Client.SpellAdmissionSyncRoot) || !Monitor.IsEntered(binding.Player.SpellAdmissionSyncRoot) ||
                !binding.Store.IsBoundSpellOwnerCurrent(binding) || !binding.Store.TryGet(binding.Connection, out var current) ||
                !ReferenceEquals(current, expected) || !ReferenceEquals(binding.Player.ownedCpsBinding, binding) ||
                checked((uint)expected.Character.ConquerPoints) != Volatile.Read(ref binding.Player._cps) ||
                checked((int)expected.Character.BoundConquerPoints) != Volatile.Read(ref binding.Player._bountCps) ||
                checked((ulong)expected.Character.BankConquerPoints) != Volatile.Read(ref binding.Client.ConquerPointsSave))
                throw new InvalidOperationException("ordinaryCpsPhysicalBaselineMismatch");
            return new(binding, proposed.Character);
        }

        internal bool IsCurrentPlain() => Volatile.Read(ref player._cps) == beforeCps &&
            Volatile.Read(ref player._bountCps) == beforeBound && Volatile.Read(ref client.ConquerPointsSave) == beforeBank &&
            ReferenceEquals(Volatile.Read(ref player.ownedCpsBinding), beforeBinding);

        internal void PublishPrepared()
        {
            Volatile.Write(ref player._cps, afterCps);
            Volatile.Write(ref player._bountCps, afterBound);
            Volatile.Write(ref client.ConquerPointsSave, afterBank);
            Volatile.Write(ref player.ownedCpsBinding, binding);
        }
    }
}
