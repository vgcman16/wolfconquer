using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal sealed class BoundQueryVigorPurpose { }
    private sealed class BoundQueryVigorData(WorldSession written, LegacyOrdinarySpellBinding binding,
        LegacyNativeMagicQueryClient.QueryEpoch epoch, ushort value, LegacyNativeAttributeMapProof proof)
    {
        internal readonly LegacyOrdinarySpellBinding Binding = binding;
        internal readonly LegacyNativeMagicQueryClient.QueryEpoch Epoch = epoch;
        internal readonly ushort Value = value;
        internal IReadOnlyDictionary<string, string> Map = written.Character.Attributes;
        internal LegacyNativeAttributeMapProof Proof = proof;
    }
    private static readonly ConditionalWeakTable<BoundQueryVigorPurpose, BoundQueryVigorData> BoundQueryVigors = new();

    private sealed partial class LegacyEquipmentQueryExecution
    {
        private BoundQueryVigorPurpose? queryVigorPurpose;

        // Only the private executor supplies the exact Store-written session.
        private static BoundQueryVigorPurpose IssueBoundQueryVigor(WorldSession written,
            LegacyOrdinarySpellBinding binding, LegacyNativeMagicQueryClient.QueryEpoch? epoch, ushort value)
        {
            if (epoch is null || !ReferenceEquals(written.NativeGameClient, binding.Client) ||
                written.AccountId != binding.AccountId || written.Character.Uid != binding.Uid ||
                LegacyEquipmentQueryBounds.ReadVigor(written.Character) != value)
                throw new InvalidOperationException("boundQueryVigorStageUnavailable");
            var witness = LegacyNativeAttributeMapProof.CaptureRaw(written.Character.Attributes);
            if (!witness.IsCurrentPlain()) throw new InvalidOperationException("boundQueryVigorStageChanged");
            var purpose = new BoundQueryVigorPurpose();
            BoundQueryVigors.Add(purpose, new(written, binding, epoch, value, witness));
            return purpose;
        }

        private bool IsVigorLineageCurrent(WorldSession session) => queryVigorPurpose is not { } purpose ||
            BoundQueryVigors.TryGetValue(purpose, out var data) &&
            ReferenceEquals(session.NativeGameClient, data.Binding.Client) && session.AccountId == data.Binding.AccountId &&
            session.Character.Uid == data.Binding.Uid && ReferenceEquals(session.Character.Attributes, data.Map) &&
            data.Proof.IsCurrentPlain();

        private void RetireVigorLineage()
        {
            if (queryVigorPurpose is { } purpose) BoundQueryVigors.Remove(purpose);
            queryVigorPurpose = null;
        }

        // Before is read inside the actual Store update and after is the exact
        // Store-written result of that update, not a later refreshed holder.
        private void AdvanceVigorLineage(WorldSession before, WorldSession after)
        {
            if (queryVigorPurpose is not { } purpose) return;
            if (!BoundQueryVigors.TryGetValue(purpose, out var data) || !IsVigorLineageCurrent(before) ||
                !ReferenceEquals(after.NativeGameClient, data.Binding.Client) || after.AccountId != data.Binding.AccountId ||
                after.Character.Uid != data.Binding.Uid || LegacyEquipmentQueryBounds.ReadVigor(after.Character) != data.Value)
            { RetireVigorLineage(); return; }
            var proof = LegacyNativeAttributeMapProof.CaptureRaw(after.Character.Attributes);
            if (!proof.IsCurrentPlain()) { RetireVigorLineage(); return; }
            data.Map = after.Character.Attributes; data.Proof = proof;
        }
    }

    internal static bool ConsumeBoundQueryVigor(BoundQueryVigorPurpose purpose,
        WorldSession current, LegacyOrdinarySpellBinding binding, LegacyNativeMagicQueryClient.QueryEpoch? epoch,
        LegacyNativeMagicQueryClient.CompletedMaximums completed)
    {
        if (!BoundQueryVigors.TryGetValue(purpose, out var data) ||
            !ReferenceEquals(data.Binding, binding) || !ReferenceEquals(data.Epoch, epoch) ||
            !ReferenceEquals(current.Character.Attributes, data.Map) || !data.Proof.IsCurrentPlain() ||
            !completed.IsCurrentPlain(binding, current) || LegacyEquipmentQueryBounds.ReadVigor(current.Character) != data.Value)
            return false;
        return BoundQueryVigors.Remove(purpose);
    }
}
