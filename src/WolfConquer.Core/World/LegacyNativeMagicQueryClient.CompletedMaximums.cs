using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMagicQueryClient
{
    // Only the actual query's final native publication registers this token.
    // Constructing a token, copying equal caches, or starting a partial query
    // cannot establish or retain its private completion association.
    internal sealed partial class CompletedMaximums
    {
        // Read-only, field-specific continuation of this actual completed
        // epoch. HP/mana maximums and unrelated overlays are not Vigor inputs.
        internal bool IsVigorEpochCurrentPlain(LegacyOrdinarySpellBinding binding, WorldSession current) =>
            MaximumCompletions.TryGetValue(this, out var completed) && completed.Published &&
            ReferenceEquals(completed.Binding, binding) && DerivedQueryOwnerIsCurrentPlain(binding) &&
            CurrentMaximumCompletions.TryGetValue(completed.Entry, out var retained) && ReferenceEquals(retained, this) &&
            ReferenceEquals(completed.Entry.Epoch, completed.Epoch) &&
            completed.Source.Matches(binding.Store, binding.Connection, binding.Uid, binding.NativeIdentity, completed.Entry) &&
            current.AccountId == completed.Session.AccountId && current.Character.Uid == completed.Session.Character.Uid &&
            current.Character.Class == completed.Session.Character.Class &&
            ReferenceEquals(current.NativeGameClient, completed.Session.NativeGameClient) &&
            ReferenceEquals(current.PlayerItems, completed.Session.PlayerItems) &&
            ReferenceEquals(current.PlayerSpells, completed.Session.PlayerSpells) &&
            current.AlternateEquipmentActive == completed.Source.Alternate &&
            binding.Store.TryGet(binding.Connection, out var actual) && ReferenceEquals(actual, current) &&
            ReferenceEquals(binding.Client.Status, completed.Status);

        internal bool CoversVigorSuccessor(LegacyOrdinarySpellBinding binding, WorldSession expected, WorldSession proposed) =>
            IsVigorEpochCurrentPlain(binding, expected) &&
            MaximumCompletions.TryGetValue(this, out var completed) &&
            proposed.AccountId == expected.AccountId && proposed.Character.Uid == expected.Character.Uid &&
            proposed.Character.Class == expected.Character.Class && ReferenceEquals(proposed.NativeGameClient, expected.NativeGameClient) &&
            ReferenceEquals(expected.PlayerItems, proposed.PlayerItems) &&
            ReferenceEquals(expected.PlayerSpells, proposed.PlayerSpells) &&
            expected.AlternateEquipmentActive == proposed.AlternateEquipmentActive;

        internal bool IsCurrentPlain(LegacyOrdinarySpellBinding binding, WorldSession current) =>
            MaximumCompletions.TryGetValue(this, out var completed) && completed.Published &&
            ReferenceEquals(completed.Binding, binding) && DerivedQueryOwnerIsCurrentPlain(binding) &&
            CurrentMaximumCompletions.TryGetValue(completed.Entry, out var retained) && ReferenceEquals(retained, this) &&
            ReferenceEquals(completed.Entry.Epoch, completed.Epoch) &&
            completed.Source.Matches(binding.Store, binding.Connection, binding.Uid, binding.NativeIdentity, completed.Entry) &&
            completed.Source.InputsMatch(current) && completed.Entry.Fields == completed.Fields &&
            completed.Entry.JiangHu == completed.JiangHu &&
            ReferenceEquals(LegacyNativePerfectionClient.Read(binding.Store, binding.Connection, binding.Uid, binding.NativeIdentity), completed.Perfection) &&
            LegacyNativePerfectionClient.Prestige(binding.Store, binding.Connection, binding.Uid, binding.NativeIdentity) == completed.Prestige &&
            ReferenceEquals(current.PhysicalStatusOverlay, completed.Session.PhysicalStatusOverlay) &&
            ReferenceEquals(current.EquipmentHitpointsStatus, completed.Session.EquipmentHitpointsStatus) &&
            binding.Store.TryGet(binding.Connection, out var actual) && ReferenceEquals(actual, current) &&
            binding.Client.Status is { } status && ReferenceEquals(status, completed.Status) &&
            current.PhysicalStatusOverlay is { MaxMana: { } mana } &&
            current.EquipmentHitpointsStatus is { } hp && status.MaxMana == mana && status.MaxHitpoints == hp.MaximumHitpoints;
    }

    private sealed record MaximumCompletion(LegacyOrdinarySpellBinding Binding, QueryEpoch Epoch,
        QueryState Source, Entry Entry, object Status, WorldSession Session, LegacyNativeMagicQueryFields Fields,
        LegacyNativeMagicQueryFields JiangHu, LegacyNativePerfectionSnapshot Perfection, uint Prestige)
    {
        internal bool Published;
    }
    private static readonly ConditionalWeakTable<CompletedMaximums, MaximumCompletion> MaximumCompletions = new();
    private static readonly ConditionalWeakTable<Entry, CompletedMaximums> CurrentMaximumCompletions = new();
}
