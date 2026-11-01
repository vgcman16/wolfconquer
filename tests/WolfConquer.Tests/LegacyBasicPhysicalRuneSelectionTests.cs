using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyBasicPhysicalRuneSelectionTests
{
    // DATA ONLY: native selection has *51/Archer8049 upper bounds, unlike
    // the separate managed equipment permission predicate's *57 policy.
    [Theory]
    [InlineData(1000U, 1051U, true)]
    [InlineData(1000U, 1052U, false)]
    [InlineData(8000U, 8049U, true)]
    [InlineData(8000U, 8050U, false)]
    [InlineData(16000U, 16051U, true)]
    [InlineData(16000U, 16052U, false)]
    [InlineData(0U, uint.MaxValue, true)]
    public void CapturedJobRuleRetainsTheExactNativeBounds(uint required, uint actual, bool expected) =>
        Assert.Equal(expected, LegacyNativeItemTypeRuneMetadata.EquipPassJobReq(required, actual));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualOwnedWornSelectionDistinguishesAbsenceAndPassiveEffectWithoutMintingASpell(bool equipped)
    {
        var item = new PlayerItemSnapshotItem { Uid = 1065001, ItemId = 4033301, Position = equipped ? (ushort)103 : (ushort)212,
            Durability = 1, MaximumDurability = 1, StackSize = 0, Color = 5 };
        var items = new PlayerItemSnapshot([], [], [], [], new PlayerItemSnapshotItem[] { item });
        return OwnedEquipmentBehaviorFixture.RunAsync(new(OwnedEquipmentBehaviorFixture.Character(minHitPoints: 1), items),
            Catalog(), f =>
            {
                f.ActorFrames.Clear(); var expected = f.Current(); var files = f.Data.Files();
                var binding = Assert.IsType<LegacyOrdinarySpellBinding>(expected.NativeGameClient!.OrdinarySpellBinding);
                var proof = Capture(f, expected, expectedResult: true)!;
                Assert.Equal(equipped, proof.HasDeadlySight); Assert.Equal(equipped ? (byte)1 : (byte)0, proof.Level);
                Assert.True(CheckCurrent(f, proof)); Assert.Same(expected, f.Current());
                Assert.False(expected.PlayerSpells.ContainsSpell(40333));
                Assert.DoesNotContain((ushort)40333, expected.NativeGameClient.MySpells!.ClientSpells.Keys);
                Assert.False(expected.Character.Attributes.ContainsKey("ManagedRuneSpellOrigins"));
                if (equipped)
                {
                    var row = Assert.Single(binding.ItemCatalog!.ItemsById[4033301]); var fields = Assert.IsType<string[]>(row.Fields);
                    string name = fields[1];
                    try
                    {
                        fields[1] = "UnrelatedRune"; // Declared NEGATIVE adopted-name drift only.
                        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
                        Assert.False(CheckCurrent(f, proof)); Assert.Null(Capture(f, expected, expectedResult: false));
                    }
                    finally { fields[1] = name; }
                    Assert.True(CheckCurrent(f, proof));
                    var storage = binding.Items.Rune;
                    var cacheField = typeof(LegacyNativeRuneItemStorage).GetField("objects", BindingFlags.Instance | BindingFlags.NonPublic)!;
                    var original = Assert.IsType<LegacyNativeGameItemRuntime[]>(cacheField.GetValue(storage));
                    try
                    {
                        // No positive native row is constructed. A malformed
                        // tail must not turn the public empty result into proof
                        // that the actor legitimately has no selected effect.
                        cacheField.SetValue(storage, original.Concat(new LegacyNativeGameItemRuntime[] { null! }).ToArray());
                        Assert.Empty(storage.WornObjects); Assert.Null(Capture(f, expected, expectedResult: false));
                        Assert.False(CheckCurrent(f, proof));
                    }
                    finally { cacheField.SetValue(storage, original); }
                    Assert.True(CheckCurrent(f, proof));
                }
                else
                {
                    // A stored Rune is outside IsEquipped selection. Its
                    // unused name cannot impose a new effect-metadata gate;
                    // existing native constructor/graph proofs still apply.
                    var row = Assert.Single(binding.ItemCatalog!.ItemsById[4033301]); var fields = Assert.IsType<string[]>(row.Fields);
                    string name = fields[1];
                    try
                    {
                        fields[1] = "";
                        Assert.True(CheckCurrent(f, proof));
                        Assert.False(Capture(f, expected, expectedResult: true)!.HasDeadlySight);
                    }
                    finally { fields[1] = name; }
                }
                f.Data.AssertFiles(files); Assert.Empty(f.ActorFrames); Assert.Same(expected, f.Current());
                return Task.CompletedTask;
            });
    }

    private static LegacyBasicPhysicalRuneSelection? Capture(OwnedEquipmentBehaviorFixture f, WorldSession expected, bool expectedResult)
    {
        var binding = expected.NativeGameClient!.OrdinarySpellBinding!; LegacyBasicPhysicalRuneSelection? captured = null;
        Assert.True(f.Store.WithCurrentNativeClientIdentity(f.Connection(OwnedEquipmentBehaviorFixture.Actor), expected.Character.Uid, identity =>
        {
            Assert.Same(binding.NativeIdentity, identity);
            lock (binding.Client.SpellAdmissionSyncRoot)
            lock (binding.Player.SpellAdmissionSyncRoot)
                Assert.Equal(expectedResult, LegacyBasicPhysicalRuneSelection.TryCapture(binding, expected, out captured));
        }));
        return captured;
    }

    private static bool CheckCurrent(OwnedEquipmentBehaviorFixture f, LegacyBasicPhysicalRuneSelection proof)
    {
        var expected = f.Current(); var binding = expected.NativeGameClient!.OrdinarySpellBinding!; bool current = false;
        Assert.True(f.Store.WithCurrentNativeClientIdentity(f.Connection(OwnedEquipmentBehaviorFixture.Actor), expected.Character.Uid, identity =>
        {
            Assert.Same(binding.NativeIdentity, identity);
            lock (binding.Client.SpellAdmissionSyncRoot)
            lock (binding.Player.SpellAdmissionSyncRoot)
                current = proof.IsCurrentPlain();
        }));
        return current;
    }

    private static WorldDataCatalog Catalog()
    {
        // Actual retained4033301 raw-self passive row. No skill catalog or
        // synthetic active mapping is needed for source IsEquipped selection.
        const string row = "4033301@@DeadlySight(+1)@@0@@0@@0@@0@@0@@0@@0@@0@@8@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@1@@0@@0@@0@@4033301@@1@@0@@0@@0@@0@@1@@800@@0@@0@@1@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@RareYellowRune@@Rare~Universal~Rune.~Passive:~Increases~your~Hit~Accuracy~by~50~pts~even~if~you~have~0~Agility.~(Next~Level:~Hit~Accuracy~+60)@@0@@0@@0@@0@@16020@@0@@0@@0@@0@@23@@0@@";
        return OwnedEquipmentBehaviorFixture.Catalog(new[] { ItemTypeParser.ParseLine(row) });
    }
}
