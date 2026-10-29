using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // Old product APIs only: the existing fixture admits an actual owned Frame
    // session; reflection selects the real private resource admission entry.
    [Fact]
    public Task OwnedGenericSameIdentityManaChangeDuringStaminaDoesNotPublishPreparedMana() =>
        RunOwnedMagicCostOutputInterleaving(root: false);

    [Fact]
    public Task OwnedRootSameIdentityManaChangeDuringStaminaDoesNotPublishPreparedMana() =>
        RunOwnedMagicCostOutputInterleaving(root: true);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task OwnedMagicCostUnrelatedCurrentEditRetainsValidWireAndOwnedIssuance(bool root) =>
        RunOwnedMagicCostOutputInterleaving(root, "benign");

    [Theory]
    [InlineData(false, "cooldown")] [InlineData(true, "cooldown")]
    [InlineData(false, "authorization")] [InlineData(true, "authorization")]
    public Task OwnedMagicCostRelevantAuthorizationOrDeadlineChangeStopsContinuation(bool root, string change) =>
        RunOwnedMagicCostOutputInterleaving(root, change);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task OwnedMagicCostCompletedStaminaReplyAllowsIndependentStaminaPublicationDuringMana(bool root) =>
        RunOwnedMagicCostOutputInterleaving(root, "staminaDuringMana");

    [Fact]
    public Task OwnedMagicCostCompletedManaReplyAllowsIndependentManaPublicationDuringColdTime() =>
        RunOwnedMagicCostOutputInterleaving(root: false, "manaDuringColdTime");

    private static Task RunOwnedMagicCostOutputInterleaving(bool root, string change = "mana")
    {
        var id = root ? (ushort)1000 : (ushort)1045;
        var row = CreateMagicRow(id, sort: root ? (byte)1 : (byte)14,
            useMana: 50, useStamina: 10, damage: 100, range: 4, coldTime: 1000);
        var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [], [row]);
        var input = CreateSession(uid: PlayerUid, classCode: 142, mapId: 1000, minHitPoints: 1000,
            minMana: 1000, stamina: 100, spellIds: [id], playerItems: PlayerItemSnapshot.Empty);
        var clock = new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
        return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(
            new(input.Character, input.PlayerItems, input.PlayerSpells, input.PlayerProficiencies), world,
            new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock), async f =>
            {
                var before = f.Current(PlayerUid);
                var binding = before.NativeGameClient!.OrdinarySpellBinding!;
                Assert.NotNull(binding); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
                var physicalHp = binding.Player.HitPoints;
                Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(f.Store, f.Connection(PlayerUid), PlayerUid, out var identity));
                var unrelatedFiles = ProcFiles(f).Where(pair =>
                    !string.Equals(pair.Key, f.CharacterPath(PlayerUid), StringComparison.OrdinalIgnoreCase)).ToArray();
                var first = f.Frames(PlayerUid).Count;
                WorldSession? committed = null;
                WorldSession? changed = null;
                f.BeforeSend = (uid, bytes) =>
                {
                    if (changed is not null || uid != PlayerUid) return;
                    var frame = OwnedEquipmentBehaviorFixture.ReadFrame(bytes);
                    var selected = change == "manaDuringColdTime"
                        ? LegacyMagicColdTimePacket.TryParse(frame, out _)
                        : LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Updates.Any(entry =>
                            change == "staminaDuringMana"
                                ? entry.Type == LegacyMsgUpdateDataType.Mana && entry.Value == 950
                                : entry.Type == LegacyMsgUpdateDataType.Stamina && entry.Value == 90);
                    if (!selected) return;
                    Assert.Equal(change == "manaDuringColdTime" ? 2 : change == "staminaDuringMana" ? 1 : 0,
                        f.Frames(PlayerUid).Count - first);
                    Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                    committed = f.Current(PlayerUid);
                    AssertOwnedMagicCostOutputState(f, committed, 950, id);
                    Assert.True(LegacyNativeClientPublication.TryCurrent(f.Store, f.Connection(PlayerUid), PlayerUid, identity, out var sameIssuance));
                    Assert.Same(committed, sameIssuance);

                    // Genuine independently accepted current-owner publication
                    // through the same full Character writer/joint participant.
                    var proposed = CreateOwnedMagicCostOutputChange(committed, clock, id, change);
                    f.Store.Set(f.Connection(PlayerUid), proposed);
                    changed = f.Current(PlayerUid);
                    AssertOwnedMagicCostOutputState(f, changed,
                        change is "mana" or "manaDuringColdTime" ? 88U : 950U, id,
                        change == "staminaDuringMana" ? "95" : "90");
                    Assert.Same(binding, changed.NativeGameClient!.OrdinarySpellBinding);
                    Assert.True(LegacyNativeClientPublication.TryCurrent(f.Store, f.Connection(PlayerUid), PlayerUid, identity, out sameIssuance));
                    Assert.Same(changed, sameIssuance);
                };

                WorldSession? continuation = null;
                try { continuation = await InvokeOwnedMagicCostOutputAdmission(f, world, clock, root); }
                catch (WorldSessionPublicationException)
                {
                    // A typed postcommit lifecycle failure may propagate instead
                    // of returning null. All wire/fencing/accounting checks below
                    // remain mandatory for either honest interruption shape.
                }
                finally { f.BeforeSend = null; }

                var accepted = Assert.IsType<WorldSession>(committed);
                var independentlyPublished = Assert.IsType<WorldSession>(changed);
                var frames = f.Frames(PlayerUid).Skip(first).ToArray();
                if (change is "benign" or "staminaDuringMana" or "manaDuringColdTime")
                {
                    Assert.Same(independentlyPublished, continuation);
                    Assert.Same(continuation, f.Current(PlayerUid));
                    if (change == "benign")
                    {
                        Assert.Equal((uint?)17, independentlyPublished.ActiveNpcId);
                        Assert.Equal("retained", independentlyPublished.Character.Attributes["AfterAdmissionOpaque"]);
                        Assert.True(independentlyPublished.MagicCooldowns!.ContainsKey(62000));
                    }
                    AssertOwnedMagicCostOutputWire(world, before, id, root, frames);
                    Assert.Same(binding, continuation!.NativeGameClient!.OrdinarySpellBinding);
                    Assert.False(binding.IsRetired); Assert.False(f.Recovery.IsBlocked);
                    Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
                    AssertOwnedMagicCostOutputState(f, continuation, change == "manaDuringColdTime" ? 88U : 950U, id,
                        change == "staminaDuringMana" ? "95" : "90");
                    foreach (var pair in unrelatedFiles) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
                    Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
                    return;
                }
                Assert.DoesNotContain(frames, bytes =>
                    LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var update) &&
                    update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Mana && entry.Value == 950));
                Assert.DoesNotContain(frames, bytes =>
                    LegacyMagicColdTimePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _));
                Assert.Equal(LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(PlayerUid, 90).ToFrameBytes(), Assert.Single(frames));
                Assert.Null(continuation);
                Assert.Equal("90", accepted.Character.Attributes["Stamina"]);
                Assert.Equal("90", independentlyPublished.Character.Attributes["Stamina"]);
                Assert.Equal(accepted.MagicCooldowns![id].AddSeconds(change == "cooldown" ? 1 : 0),
                    independentlyPublished.MagicCooldowns![id]);
                var saved = f.Data.Loader.LoadByAccountId(PlayerUid)!;
                Assert.Equal(change == "mana" ? 88L : 950L, saved.MinMana);
                Assert.Equal(before.Character.MinHitPoints, saved.MinHitPoints);
                Assert.Equal(change == "mana" ? 88U : 950U, binding.Player.Mana);
                Assert.Equal(physicalHp, binding.Player.HitPoints);
                Assert.Equal(change == "authorization" ? (ushort)17 : accepted.LegacyRandomSpell, independentlyPublished.LegacyRandomSpell);
                Assert.Equal(before.Character.MinHitPoints, independentlyPublished.Character.MinHitPoints);
                Assert.Equal(before.Character.Money, independentlyPublished.Character.Money);
                Assert.Equal(before.Character.ConquerPoints, independentlyPublished.Character.ConquerPoints);
                Assert.Same(before.PlayerItems, independentlyPublished.PlayerItems);
                Assert.Same(before.PlayerSpells, independentlyPublished.PlayerSpells);
                Assert.Same(before.PlayerProficiencies, independentlyPublished.PlayerProficiencies);
                foreach (var pair in unrelatedFiles) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
                Assert.True(binding.IsRetired); Assert.True(f.Recovery.IsBlocked);
                Assert.False(f.Store.TryGet(f.Connection(PlayerUid), out _));
                Assert.False(LegacyNativeClientPublication.TryCurrent(f.Store, f.Connection(PlayerUid), PlayerUid, identity, out _));
                Assert.ThrowsAny<InvalidOperationException>(() => f.Store.Set(f.Connection(PlayerUid), before));
                Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            });
    }

    private static WorldSession CreateOwnedMagicCostOutputChange(WorldSession committed,
        ManualRuntimeTimeProvider clock, ushort id, string change) => change switch
    {
        "mana" or "manaDuringColdTime" => committed with { Character = committed.Character with { MinMana = 88 } },
        "staminaDuringMana" => committed with
        {
            Character = committed.Character with
            {
                Attributes = new Dictionary<string, string>(committed.Character.Attributes) { ["Stamina"] = "95" },
            },
        },
        "benign" => committed with
        {
            ActiveNpcId = 17,
            Character = committed.Character with
            {
                Attributes = new Dictionary<string, string>(committed.Character.Attributes)
                    { ["AfterAdmissionOpaque"] = "retained" },
            },
            MagicCooldowns = new Dictionary<ushort, DateTimeOffset>(committed.MagicCooldowns!)
                { [62000] = clock.GetUtcNow().AddMinutes(1) },
        },
        "cooldown" => committed with
        {
            MagicCooldowns = new Dictionary<ushort, DateTimeOffset>(committed.MagicCooldowns!)
                { [id] = committed.MagicCooldowns![id].AddSeconds(1) },
        },
        "authorization" => committed with { LegacyRandomSpell = 17 },
        _ => throw new ArgumentOutOfRangeException(nameof(change)),
    };

    private static void AssertOwnedMagicCostOutputWire(WorldDataCatalog world, WorldSession before,
        ushort id, bool root, byte[][] frames)
    {
        Assert.Equal(root ? 2 : 3, frames.Length);
        Assert.Equal(LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(PlayerUid, 90).ToFrameBytes(), frames[0]);
        Assert.Equal(new LegacyMsgUpdatePacket(PlayerUid, [new(LegacyMsgUpdateDataType.Mana, 950)]).ToFrameBytes(), frames[1]);
        if (root) return;
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(before.PlayerSpells, world, id, out _, out var spell));
        Assert.Equal(LegacyMagicColdTimePacket.FromStartedCooldown(spell).ToFrameBytes(), frames[2]);
    }

    private static void AssertOwnedMagicCostOutputState(OwnedEquipmentBehaviorFixture f,
        WorldSession session, uint mana, ushort id, string stamina = "90")
    {
        Assert.Equal((long)mana, session.Character.MinMana);
        Assert.Equal(mana, session.NativePlayer!.Mana);
        Assert.Equal((long)mana, f.Data.Loader.LoadByAccountId(PlayerUid)!.MinMana);
        Assert.Equal(stamina, session.Character.Attributes["Stamina"]);
        Assert.True(session.MagicCooldowns!.ContainsKey(id));
    }

    private static async Task<WorldSession?> InvokeOwnedMagicCostOutputAdmission(
        OwnedEquipmentBehaviorFixture f, WorldDataCatalog world, ManualRuntimeTimeProvider clock, bool root)
    {
        var owner = f.Current(PlayerUid);
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(owner.PlayerSpells, world,
            root ? (ushort)1000 : (ushort)1045, out _, out var spell));
        var method = typeof(WorldPacketHandlers).GetMethod(
            root ? "TryPrepareLegacyRootFirstFamilyCanUseAsync" : "TryPrepareLegacyPlayerMagicCastAsync",
            BindingFlags.Static | BindingFlags.NonPublic, null,
            [typeof(WorldPacketHandlerContext), typeof(WorldSession), typeof(LegacyMagicRuntimeSpell),
             typeof(CancellationToken), typeof(object)], null)!;
        object? result;
        try { result = method.Invoke(null, [ProcContext(f, world, clock, PlayerUid), owner, spell, CancellationToken.None, null]); }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        { ExceptionDispatchInfo.Capture(inner).Throw(); throw; }
        return await Assert.IsType<ValueTask<WorldSession?>>(result);
    }
}
