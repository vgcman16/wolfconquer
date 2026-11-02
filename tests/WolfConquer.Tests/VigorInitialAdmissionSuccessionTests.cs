using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class VigorInitialAdmissionSuccessionTests
{
    private const uint Actor = OwnedEquipmentBehaviorFixture.Actor;
    private static OwnedEquipmentBehaviorInput Input()
    {
        var character = OwnedEquipmentBehaviorFixture.Character(classCode: 14005, agility: 0, strength: 0, minHitPoints: 30)
            with { Vitality = 0, Spirit = 10, AttributePoints = 12,
                Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Vigor"] = "0" } };
        return new(character, PlayerItemSnapshot.Empty);
    }

    private static bool CurrentVigorProof(OwnedEquipmentBehaviorFixture f) =>
        f.ObserveCurrentVigorQualification();

    [Fact]
    public Task GenuineAdmissionSeedSurvivesActualUnrelatedStorePublicationBeforeQuery()
    {
        bool observed = false;
        return OwnedEquipmentBehaviorFixture.RunObservingInitialAdmissionAsync(Input(), OwnedEquipmentBehaviorFixture.Catalog(),
            (f, uid, _) =>
            {
                if (uid != Actor || observed) return;
                observed = true;
                var admitted = f.Current();
                Assert.True(CurrentVigorProof(f));
                var files = f.Data.Files();
                var fields = new Dictionary<string, string>(admitted.Character.Attributes, StringComparer.Ordinal)
                    { ["Other"] = "one", ["other"] = "two" };
                f.Store.Set(f.Connection(Actor), admitted with { Character = admitted.Character with { Attributes = fields } });
                Assert.True(CurrentVigorProof(f));
                f.Data.AssertFiles(files);
            }, f =>
            {
                Assert.True(observed);
                Assert.True(CurrentVigorProof(f)); // Actual later query completion supersedes seed.
                return Task.CompletedTask;
            });
    }

    [Fact]
    public Task ActualInFlightQueryRetiresSeedUntilRealQueryCompletion()
    {
        bool observed = false;
        return OwnedEquipmentBehaviorFixture.RunObservingInitialAdmissionAsync(Input(), OwnedEquipmentBehaviorFixture.Catalog(),
            (f, uid, bytes) =>
            {
                if (uid != Actor || observed || !LegacyUserTotalRefineLevPacket.TryParse(
                        OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _)) return;
                observed = true;
                Assert.False(CurrentVigorProof(f));
            }, f =>
            {
                Assert.True(observed);
                Assert.True(CurrentVigorProof(f));
                return Task.CompletedTask;
            });
    }

    [Fact]
    public Task ActualPreQueryVigorChangeAndRestoreCannotReissueSeed()
    {
        bool observed = false;
        return OwnedEquipmentBehaviorFixture.RunObservingInitialAdmissionAsync(Input(), OwnedEquipmentBehaviorFixture.Catalog(),
            (f, uid, _) =>
            {
                if (uid != Actor || observed) return;
                observed = true;
                Assert.True(CurrentVigorProof(f));
                var first = f.Current();
                var one = new Dictionary<string, string>(first.Character.Attributes, StringComparer.OrdinalIgnoreCase) { ["Vigor"] = "1" };
                f.Store.Set(f.Connection(Actor), first with { Character = first.Character with { Attributes = one } });
                var second = f.Current();
                var restored = new Dictionary<string, string>(second.Character.Attributes, StringComparer.OrdinalIgnoreCase) { ["Vigor"] = "0" };
                f.Store.Set(f.Connection(Actor), second with { Character = second.Character with { Attributes = restored } });
                Assert.False(CurrentVigorProof(f));
            }, f =>
            {
                Assert.True(observed);
                Assert.True(CurrentVigorProof(f)); // Only the completed genuine query can restore authority.
                return Task.CompletedTask;
            });
    }
}
