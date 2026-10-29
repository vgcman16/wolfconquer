using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData("-1", false)] [InlineData("", true)] [InlineData("not-a-number", false)]
    [InlineData("18446744073709551616", true)]
    public Task OwnedGenericMalformedSavedStaminaRefusesWithoutDefaultResources(string raw, bool first) =>
        RunStaminaAdmissionOwner(raw, "1", first, 1000, async (f, world, clock) =>
        {
            var owner = f.Current(PlayerUid); var peer = f.Current(TargetPlayerUid);
            StaminaInput(f, raw); var native = StaminaNative(f); var files = ProcFiles(f); int ownFrames = f.Frames(PlayerUid).Count, peerFrames = f.Frames(TargetPlayerUid).Count;
            Assert.Null(await InvokeStaminaAdmission(f, world, clock, root: false));
            StaminaRefusal(f, owner, peer, files, ownFrames, peerFrames);
            Assert.Equal(native, StaminaNative(f));
        });

    [Theory]
    [InlineData("-1", true)] [InlineData("", false)] [InlineData("not-a-number", true)]
    [InlineData("18446744073709551616", false)]
    public Task OwnedRootMalformedSavedStaminaRefusesWithoutDefaultResources(string raw, bool first) =>
        RunStaminaAdmissionOwner(raw, "1", first, 1000, async (f, world, clock) =>
        {
            var owner = f.Current(PlayerUid); var peer = f.Current(TargetPlayerUid);
            StaminaInput(f, raw); var native = StaminaNative(f); var files = ProcFiles(f); int ownFrames = f.Frames(PlayerUid).Count, peerFrames = f.Frames(TargetPlayerUid).Count;
            Assert.Null(await InvokeStaminaAdmission(f, world, clock, root: true));
            StaminaRefusal(f, owner, peer, files, ownFrames, peerFrames);
            Assert.Equal(native, StaminaNative(f));
        });

    private static Task RunStaminaAdmissionOwner(string? raw, string? blessing, bool first, long map,
        Func<OwnedEquipmentBehaviorFixture, WorldDataCatalog, ManualRuntimeTimeProvider, Task> body, byte cost = 10)
    {
        var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
            [CreateMagicRow(1000, sort: 1, useMana: 50, useStamina: cost, damage: 100, range: 4, coldTime: 1000),
             CreateMagicRow(1045, sort: 14, useMana: 50, useStamina: cost, damage: 100, range: 4, coldTime: 1000)]);
        OwnedEquipmentBehaviorInput Input(uint uid)
        {
            var original = CreateSession(uid: uid, classCode: 142, mapId: map,
                x: uid == PlayerUid ? 830 : 831, y: 584, minHitPoints: 1000, minMana: 1000, stamina: 100,
                spellIds: [1000, 1045], attributes: new Dictionary<string, string> { ["PkMode"] = "0", ["DynamicID"] = map == 1039 ? "1" : "0" });
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (first && raw is not null) attributes["Stamina"] = raw;
            attributes["StaminaOrderWitness"] = "preserved";
            foreach (var pair in original.Character.Attributes.Where(pair => !string.Equals(pair.Key, "Stamina", StringComparison.OrdinalIgnoreCase)))
                attributes[pair.Key] = pair.Value;
            if (blessing is not null) attributes["HeavenBlessing"] = blessing;
            if (!first && raw is not null) attributes["Stamina"] = raw;
            return new(original.Character with { Attributes = attributes }, PlayerItemSnapshot.Empty, original.PlayerSpells);
        }
        var clock = new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
        return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(Input(PlayerUid), world,
            new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock), async f =>
            {
                if (raw is not null)
                    foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
                    {
                        var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath(uid)), out _);
                        var staminaRow = text.IndexOf("\nStamina=", StringComparison.Ordinal);
                        var witness = text.IndexOf("\nStaminaOrderWitness=", StringComparison.Ordinal);
                        Assert.True(staminaRow >= 0 && witness >= 0);
                        Assert.Equal(first, staminaRow < witness); // Actual retained file order, not a dictionary-only proxy.
                    }
                await body(f, world, clock);
            }, Input(TargetPlayerUid));
    }

    private static void StaminaInput(OwnedEquipmentBehaviorFixture f, string raw)
    {
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            Assert.True(f.Current(uid).Character.Attributes.ContainsKey("Stamina"));
            Assert.Equal(raw, f.Current(uid).Character.Attributes["Stamina"]);
            Assert.Equal(raw, f.Data.Loader.LoadByAccountId(uid)!.Attributes["Stamina"]);
            Assert.NotNull(f.Current(uid).NativeGameClient!.OrdinarySpellBinding);
        }
    }
    private static (int Hp, uint Mana, ushort Stamina, ushort Authorization, ushort Soul)[] StaminaNative(OwnedEquipmentBehaviorFixture f) =>
        new[] { f.Current(PlayerUid), f.Current(TargetPlayerUid) }.Select(session =>
            (session.NativePlayer!.HitPoints, session.NativePlayer.Mana, session.NativePlayer.Stamina,
                session.NativePlayer.RandomSpell, session.NativeGameClient!.OnSoulSpell)).ToArray();
    private static void StaminaRefusal(OwnedEquipmentBehaviorFixture f, WorldSession owner, WorldSession peer,
        Dictionary<string, byte[]> files, int ownerFrames, int peerFrames)
    {
        Assert.Same(owner, f.Current(PlayerUid)); Assert.Same(peer, f.Current(TargetPlayerUid));
        Assert.Equal(ownerFrames, f.Frames(PlayerUid).Count); Assert.Equal(peerFrames, f.Frames(TargetPlayerUid).Count);
        Assert.Null(owner.MagicCooldowns); Assert.Null(peer.MagicCooldowns);
        Assert.Equal(1000L, owner.Character.MinMana); Assert.Equal(1000U, owner.NativePlayer!.Mana);
        Assert.Equal(1000L, f.Data.Loader.LoadByAccountId(PlayerUid)!.MinMana);
        Assert.Equal((ushort)0, owner.LegacyRandomSpell); Assert.Same(owner.NativeGameClient!.OrdinarySpellBinding, f.Current(PlayerUid).NativeGameClient!.OrdinarySpellBinding);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(owner.NativeGameClient.OrdinarySpellBinding!));
        ProcFilesUnchanged(f, files); Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }
    private static async Task<WorldSession?> InvokeStaminaAdmission(OwnedEquipmentBehaviorFixture f, WorldDataCatalog world,
        ManualRuntimeTimeProvider clock, bool root)
    {
        var owner = f.Current(PlayerUid);
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(owner.PlayerSpells, world, root ? (ushort)1000 : (ushort)1045, out _, out var spell));
        var context = ProcContext(f, world, clock, PlayerUid);
        var method = typeof(WorldPacketHandlers).GetMethod(root ? "TryPrepareLegacyRootFirstFamilyCanUseAsync" : "TryPrepareLegacyPlayerMagicCastAsync",
            BindingFlags.Static | BindingFlags.NonPublic, null,
            [typeof(WorldPacketHandlerContext), typeof(WorldSession), typeof(LegacyMagicRuntimeSpell), typeof(CancellationToken), typeof(object)], null)!;
        object? result;
        try { result = method.Invoke(null, [context, owner, spell, CancellationToken.None, null]); }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        { ExceptionDispatchInfo.Capture(inner).Throw(); throw; }
        return await Assert.IsType<ValueTask<WorldSession?>>(result);
    }
}
