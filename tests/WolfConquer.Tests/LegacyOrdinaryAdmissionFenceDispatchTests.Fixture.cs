using System.Collections;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Xunit.Abstractions;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOrdinaryAdmissionFenceDispatchTests(ITestOutputHelper output)
{
    private const uint Uid = LegacyOwnedAdmissionRuntimeFixture.Uid;
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

    // Every entry under observation was issued by the actual registry and
    // TryCreate. Reflection observes private DATA dispatch only; it never
    // supplies an owner, changes membership, or grants a write/recovery permit.
    private static Array Bucket(string directory) => Assert.IsAssignableFrom<Array>(
        typeof(LegacyOrdinaryAdmissionMutationFence).GetMethod("SnapshotParticipants", PrivateStatic)!
            .Invoke(null, [directory]));

    private static (LegacyOrdinaryAdmissionUidOwner Owner, LegacyTradeJournalFileKind Kind)[] Rows(Array bucket) =>
        bucket.Cast<object>().Select(row =>
            ((LegacyOrdinaryAdmissionUidOwner)row.GetType().GetProperty("Owner")!.GetValue(row)!,
             (LegacyTradeJournalFileKind)row.GetType().GetProperty("Kind")!.GetValue(row)!)).ToArray();

    private static int RegisteredCount()
    {
        var fence = typeof(LegacyOrdinaryAdmissionMutationFence);
        var gate = fence.GetField("Gate", PrivateStatic)!.GetValue(null)!;
        lock (gate) return ((IDictionary)fence.GetField("Owners", PrivateStatic)!.GetValue(null)!).Count;
    }

    private static (LegacyOrdinaryAdmissionUidOwner? First, LegacyOrdinaryAdmissionUidOwner? Second)
        SessionOwners(string? previous, string? next) =>
        ((LegacyOrdinaryAdmissionUidOwner?, LegacyOrdinaryAdmissionUidOwner?))
            typeof(LegacyOrdinaryAdmissionMutationFence).GetMethod("SnapshotSessionOwners", PrivateStatic)!
                .Invoke(null, [previous, next])!;

    private sealed class OwnerFixture : IDisposable
    {
        internal LegacyOwnedAdmissionRuntimeFixture Data { get; } = new();
        internal LegacyItemUidRegistry Registry { get; private set; } = null!;
        internal LegacyOrdinaryAdmissionUidOwner Owner { get; private set; } = null!;
        internal LegacyOrdinaryAdmissionRoots Roots { get; private set; } = null!;

        internal OwnerFixture(string? sharedSpells = null, string? sharedGear = null)
        {
            try
            {
                Roots = Data.Options().Roots with
                {
                    Spells = sharedSpells ?? Data.Spells,
                    CombatGear = sharedGear ?? Data.CombatGear,
                };
                Open(Roots);
            }
            catch { Registry?.Dispose(); Data.Dispose(); throw; }
        }

        internal void Open(LegacyOrdinaryAdmissionRoots roots)
        {
            Registry?.Dispose();
            Assert.True(LegacyItemUidRegistry.TryOpen(Data.RegistryOptions(), Data.ExpectedDomain,
                out var opened, out var reason), reason);
            Registry = Assert.IsType<LegacyItemUidRegistry>(opened);
            Assert.True(LegacyOrdinaryAdmissionUidOwner.TryCreate(Registry, Data.ExpectedDomain, roots,
                Data.Mailbox, out var owner, out reason), reason);
            Owner = Assert.IsType<LegacyOrdinaryAdmissionUidOwner>(owner);
            Roots = roots;
            LegacyOrdinaryAdmissionMutationFence.BindConfiguredDomain(roots);
            Assert.True(Owner.IsCurrent(roots));
            Assert.False(Owner.IsPending(Uid));
        }

        internal void SaveSameProficiencies() => new PlayerProficiencySnapshotLoader(Roots.Proficiencies!)
            .SaveByCharacterUid(Uid, PlayerProficiencySnapshot.Empty);

        // These sessions are DATA for the fence's existing decision contract,
        // never injected into a Store or treated as native admission authority.
        internal WorldSession Session(long account = Uid) => new(account, "FenceData",
            Data.Loader.TryLoadByAccountIdForAdmission(Uid).Snapshot!, PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty, UsersRoot: Roots.Users);

        public void Dispose()
        {
            try { Registry?.Dispose(); }
            finally { Data.Dispose(); }
        }
    }
}
