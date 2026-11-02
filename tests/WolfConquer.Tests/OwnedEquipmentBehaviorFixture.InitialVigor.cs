using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal sealed partial class OwnedEquipmentBehaviorFixture
{
    // Observe through the same real Store/client/player/storage gate order as
    // attribute allocation. This does not create query or publication authority.
    internal bool ObserveCurrentVigorQualification(WorldSession? candidate = null)
    {
        var current = candidate ?? Current();
        var binding = Current().NativeGameClient!.OrdinarySpellBinding!;
        var disk = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(
            File.ReadAllBytes(CharacterPath()), out _));
        bool valid = false;
        Assert.True(Store.WithCurrentNativeClientIdentity(Connection(Actor), Actor, identity =>
        {
            Assert.Same(binding.NativeIdentity, identity);
            lock (binding.Client.SpellAdmissionSyncRoot)
            lock (binding.Player.SpellAdmissionSyncRoot)
            lock (binding.Storage.MutationGate)
            lock (binding.Proficiencies.MutationGate)
                valid = binding.MatchesAttributeAllocationVigor(disk, current);
        }));
        return valid;
    }

    // Actual owned Frame admission with observation only at its real send
    // route. No detached/accepting replacement owner or saved session is used.
    internal static async Task RunObservingInitialAdmissionAsync(OwnedEquipmentBehaviorInput actor,
        WorldDataCatalog catalog, Action<OwnedEquipmentBehaviorFixture, uint, byte[]> observe,
        Func<OwnedEquipmentBehaviorFixture, Task> exercise)
    {
        OwnedEquipmentBehaviorFixture? fixture = null;
        Exception? primary = null;
        try
        {
            var active = new OwnedEquipmentBehaviorFixture();
            fixture = active;
            active.BeforeSend = (uid, bytes) => observe(active, uid, bytes);
            await active.InitializeAsync(actor, catalog, LegacyRuntimeUpdateLoopOptions.Disabled, [], false, null);
            active.BeforeSend = null;
            await exercise(active);
        }
        catch (Exception error) { primary = error; }
        Exception? cleanup = null;
        if (fixture is not null)
            try { fixture.BeforeSend = null; fixture.RetainEvidence = primary is not null; await fixture.DisposeAsync(); }
            catch (Exception error) { cleanup = error; }
        if (primary is not null && cleanup is not null) throw new AggregateException(primary, cleanup);
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
    }
}
