using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    // Genuine admitted owners, DATA witnesses only. None of these calls
    // publishes a spell, death result, attack entry or native readiness.
    [Fact]
    public Task ActualOwnerRawRuntimeAndDerivedRowsRemainValidWithoutFillingNativeCaches() => RunMetadata((f, catalog) =>
    {
        var runtime = Assert.Single(catalog.MagicByTypeId[15920]);
        var derived = Assert.Single(catalog.MagicByTypeId[21930]);
        Assert.False(catalog.TryReadOwnedPhysicalMagicRow(runtime, out _));
        Assert.False(catalog.TryReadOwnedPhysicalMagicRow(derived, out _));
        WithMetadataOwner(f, (binding, current) =>
        {
            var proof = LegacyOwnedPhysicalMetadataWitness.Capture(binding, catalog, current);
            Assert.True(LegacyMagicRuntime.TryResolveSpell(catalog, 15920, 9, out var raw));
            Assert.Null(raw.IsRuneSkill); // Actual descriptor has no IsRuneSkill declaration.
            proof.RequireRuntimeSpell(raw);
            Assert.True(LegacyMagicRuntime.TryResolveSpell(catalog, 21930, 1, out _));
            proof.RequireDerivedSpell(21930, 1);
            Assert.True(proof.IsCurrentPlain());
            var exported = Assert.Single(proof.CaptureDerivedSpellRows());
            Assert.Equal(21930, exported.TypeId);
            Assert.Equal(derived.Fields.ToArray(), exported.Fields.ToArray());
            Assert.NotSame(derived, exported); Assert.NotSame(derived.Fields, exported.Fields);
        });
        Assert.False(catalog.TryReadOwnedPhysicalMagicRow(runtime, out _));
        Assert.False(catalog.TryReadOwnedPhysicalMagicRow(derived, out _));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualOwnerUsedRawRuntimeOrDerivedDriftCannotReuseTheEarlierWitness(bool derived) => RunMetadata((f, catalog) =>
    {
        ushort id = derived ? (ushort)21930 : (ushort)15920;
        ushort level = derived ? (ushort)1 : (ushort)9;
        var row = Assert.Single(catalog.MagicByTypeId[id]); var fields = Assert.IsType<string[]>(row.Fields);
        var original = fields[10];
        WithMetadataOwner(f, (binding, current) =>
        {
            var proof = LegacyOwnedPhysicalMetadataWitness.Capture(binding, catalog, current);
            Assert.True(LegacyMagicRuntime.TryResolveSpell(catalog, id, level, out var actualResolved));
            RequireRaw(proof, actualResolved, derived); Assert.True(proof.IsCurrentPlain());
            try
            {
                fields[10] = "101"; // Declared escaped used-payload edit after genuine capture.
                Assert.False(proof.IsCurrentPlain());
                Assert.Throws<InvalidDataException>(() => RequireRaw(proof, actualResolved, derived));
                if (!derived)
                {
                    // A valid changed raw row still cannot justify a value
                    // resolved before that change, even on a fresh witness.
                    var changed = LegacyOwnedPhysicalMetadataWitness.Capture(binding, catalog, current);
                    Assert.Throws<InvalidDataException>(() => changed.RequireRuntimeSpell(actualResolved));
                }
                fields[10] = "NaN";
                var malformed = LegacyOwnedPhysicalMetadataWitness.Capture(binding, catalog, current);
                Assert.Throws<InvalidDataException>(() => RequireRaw(malformed, actualResolved, derived));
                Assert.False(catalog.TryReadOwnedPhysicalMagicRow(row, out _));
            }
            finally { fields[10] = original; }
            Assert.True(proof.IsCurrentPlain());
        });
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualNativeLoadedPurposePinsOnlyItsReturnedMutableRowWhileRawPurposeStaysIndependent(bool cooldown) =>
        RunMetadata((f, catalog) =>
        {
            var row = Assert.Single(catalog.MagicByTypeId[15920]);
            Assert.False(catalog.TryReadOwnedPhysicalMagicRow(row, out _));
            WithMetadataOwner(f, (binding, current) =>
            {
                var raw = LegacyOwnedPhysicalMetadataWitness.Capture(binding, catalog, current);
                Assert.True(LegacyMagicRuntime.TryResolveSpell(catalog, 15920, 9, out var resolved));
                raw.RequireRuntimeSpell(resolved);
                Assert.False(catalog.TryReadOwnedPhysicalMagicRow(row, out _));
                Assert.True(LegacyMagicRuntime.TryResolveNativeLoadedSpell(catalog, 15920, 9, out _, out var actual));
                var loaded = LegacyOwnedPhysicalMetadataWitness.Capture(binding, catalog, current);
                loaded.RequireNativeLoadedSpell(15920, 9, actual); Assert.True(loaded.IsCurrentPlain());
                var damage = actual.Damage; var oldCooldown = actual.CoolDown;
                try
                {
                    if (cooldown) actual.SetCoolDown(checked((ushort)(oldCooldown + 1)));
                    else actual.SetDamage(damage + 1);
                    Assert.False(loaded.IsCurrentPlain());
                    Assert.True(raw.IsCurrentPlain());
                    Assert.Throws<InvalidDataException>(() => loaded.RequireNativeLoadedSpell(15920, 9, actual));
                }
                finally { actual.SetDamage(damage); actual.SetCoolDown(oldCooldown); }
                Assert.True(loaded.IsCurrentPlain()); Assert.True(raw.IsCurrentPlain());
            });
        });

    private static void RequireRaw(LegacyOwnedPhysicalMetadataWitness proof, LegacyMagicRuntimeSpell actualResolved, bool derived)
    {
        if (derived) proof.RequireDerivedSpell(actualResolved.Id, actualResolved.Level);
        else proof.RequireRuntimeSpell(actualResolved);
    }

    private static Task RunMetadata(Action<OwnedEquipmentBehaviorFixture, WorldDataCatalog> body)
    {
        var runtime = HawksCatalog();
        // Exact retained DBServer/magictype.txt line4119:21930 level1. The
        // actual parser/adopted catalog are used, not a cache/default row.
        const string row = "21930@@21930@@6@@Dark@@0@@0@@0@@0@@1@@0@@1@@0@@200@@1@@0@@16@@417@@0@@0@@0@@0@@0@@0@@0@@48@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@416@@1@@50@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@";
        var catalog = NativeCallerWorldDataFixture.Create(new ItemType[] { }, [],
            magic: new[] { Assert.Single(runtime.Magic), MagicTypeParser.ParseLine(row) }, magicEffects: runtime.MagicEffects);
        return OwnedEquipmentBehaviorFixture.RunAsync(new(Saved(Actor, "MetadataOwner", 830, 5000, 300, false),
            PlayerItemSnapshot.Empty), catalog, f =>
        {
            var current = f.Current(); var binding = Assert.IsType<LegacyOrdinarySpellBinding>(current.NativeGameClient!.OrdinarySpellBinding);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            var files = f.Data.Files(); var image = binding.Items.Image; var spells = binding.Storage.ClientSpells;
            f.ActorFrames.Clear(); body(f, catalog);
            f.Data.AssertFiles(files); Assert.Same(current, f.Current());
            Assert.Same(image, binding.Items.Image); Assert.Same(spells, binding.Storage.ClientSpells);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.Empty(f.ActorFrames);
            Assert.Equal(checked((int)current.Character.MinHitPoints), binding.Player.HitPoints);
            Assert.Equal(checked((uint)current.Character.MinMana), binding.Player.Mana);
            Assert.Equal(LegacyStaminaState.RequireCurrent(current.Character), binding.Player.Stamina);
            Assert.Equal(LegacyActionState.RequireCurrent(current.Character), (uint)binding.Player.Action);
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            return Task.CompletedTask;
        });
    }

    private static void WithMetadataOwner(OwnedEquipmentBehaviorFixture f, Action<LegacyOrdinarySpellBinding, WorldSession> body)
    {
        var current = f.Current(); var binding = Assert.IsType<LegacyOrdinarySpellBinding>(current.NativeGameClient!.OrdinarySpellBinding);
        Assert.True(f.Store.WithCurrentNativeClientIdentity(f.Connection(Actor), current.Character.Uid, identity =>
        {
            Assert.Same(binding.NativeIdentity, identity);
            lock (binding.Client.SpellAdmissionSyncRoot)
            lock (binding.Player.SpellAdmissionSyncRoot)
                body(binding, current);
        }));
    }
}
