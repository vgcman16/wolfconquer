using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // Old-API controls: genuine saved admission/Frame Store/registry, then the
    // actual private admission entry. No fabricated native owners or saver.
    [Fact]
    public Task OwnedStarFlowManaRefusalPreservesStaminaSavedManaCooldownAndOutput() =>
        RunOwnedProcAsync(10950, 49, async (f, world, clock) =>
        {
            var before = f.Current(PlayerUid); var files = ProcFiles(f); int count = f.Frames(PlayerUid).Count;
            Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(before.PlayerSpells, world, 10950, out _, out var spell));
            var admitted = await InvokeProcAdmission("TryAdmitLegacyStoneCrackerStarFlowSpellAsync",
                [ProcContext(f, world, clock, PlayerUid), before, spell, CancellationToken.None], 2);
            Assert.False(admitted); Assert.Same(before, f.Current(PlayerUid));
            Assert.Equal("100", f.Current(PlayerUid).Character.Attributes["Stamina"]);
            Assert.Equal(49L, f.Current(PlayerUid).Character.MinMana); Assert.Null(f.Current(PlayerUid).MagicCooldowns);
            Assert.Equal(count, f.Frames(PlayerUid).Count); ProcFilesUnchanged(f, files);
        });

    [Fact]
    public Task OwnedNestedManaRefusalPreservesStaminaSavedManaCooldownAndOutput() =>
        RunOwnedProcAsync(20220, 49, async (f, world, clock) =>
        {
            var owner = f.Current(TargetPlayerUid); var attacker = f.Current(PlayerUid); var files = ProcFiles(f);
            int count = f.Frames(TargetPlayerUid).Count;
            Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(owner.PlayerSpells, world, 20220, out _, out var spell));
            var admitted = await InvokeProcAdmission("TryAdmitLegacyCombatGearNestedPlayerDamageProcAsync",
                [ProcContext(f, world, clock, TargetPlayerUid), f.Connection(PlayerUid), attacker, owner, spell, CancellationToken.None], 3);
            Assert.False(admitted); Assert.Same(owner, f.Current(TargetPlayerUid)); Assert.Same(attacker, f.Current(PlayerUid));
            Assert.Equal("100", f.Current(TargetPlayerUid).Character.Attributes["Stamina"]);
            Assert.Equal(49L, f.Current(TargetPlayerUid).Character.MinMana); Assert.Null(f.Current(TargetPlayerUid).MagicCooldowns);
            Assert.Equal(count, f.Frames(TargetPlayerUid).Count); ProcFilesUnchanged(f, files);
        });

    private static Task RunOwnedProcAsync(ushort proc, long mana,
        Func<OwnedEquipmentBehaviorFixture, WorldDataCatalog, ManualRuntimeTimeProvider, Task> body, string? rawStamina = null)
    {
        var row = CreateMagicRow(proc, sort: proc == 20220 ? (byte)105 : (byte)5,
            useMana: 50, useStamina: 10, damage: 100, range: 3, coldTime: 1000, coolDown: 0);
        var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [], [row]);
        OwnedEquipmentBehaviorInput Input(uint uid)
        {
            var character = CreateSession(uid: uid, classCode: 142,
                reborn: 2, mapId: 1000, x: uid == PlayerUid ? 830 : 831, y: 584, minHitPoints: 1000,
                minMana: mana, stamina: 100, spellIds: [proc], attributes: new Dictionary<string, string>
                { ["PkMode"] = "0", ["BattlePower"] = "120", ["LegacyCombatGearMagneticBattlePowerRoll"] = "0" }).Character;
            // Seed the actual saved attribute before bootstrap/admission. Null
            // preserves every existing caller; empty is a PRESENT invalid value.
            if (rawStamina is not null)
                character = character with { Attributes = new Dictionary<string, string>(character.Attributes,
                    StringComparer.OrdinalIgnoreCase) { ["Stamina"] = rawStamina } };
            return new(character, PlayerItemSnapshot.Empty, new([CreatePlayerSpell(proc)]));
        }
        var clock = new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
        return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(Input(PlayerUid), world,
            new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock),
            f => body(f, world, clock), Input(TargetPlayerUid));
    }
    private static WorldPacketHandlerContext ProcContext(OwnedEquipmentBehaviorFixture f, WorldDataCatalog world,
        ManualRuntimeTimeProvider clock, uint uid) => new(f.Connection(uid), f.Log, f.Current(uid), WorldData: world,
            Sessions: f.Store, TimeProvider: clock, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
    private static async Task<bool> InvokeProcAdmission(string name, object?[] arguments, int admittedItem)
    {
        object? result;
        try { result = typeof(WorldPacketHandlers).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, arguments); }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        { ExceptionDispatchInfo.Capture(inner).Throw(); throw; }
        // The private abort enum stays private. Await the ACTUAL boxed
        // ValueTask<T>/Task<T>, then inspect its real tuple's admitted bool.
        Assert.NotNull(result);
        var task = Assert.IsAssignableFrom<Task>(result.GetType().GetMethod("AsTask")!.Invoke(result, null));
        await task;
        var tuple = task.GetType().GetProperty("Result")!.GetValue(task)!;
        return Assert.IsType<bool>(tuple.GetType().GetField("Item" + admittedItem)!.GetValue(tuple));
    }
    private static Dictionary<string, byte[]> ProcFiles(OwnedEquipmentBehaviorFixture f) =>
        Directory.GetFiles(f.Data.Root, "*", SearchOption.AllDirectories).Where(path => Path.GetFileName(path) != "owner.lock")
            .ToDictionary(path => path, File.ReadAllBytes);
    private static void ProcFilesUnchanged(OwnedEquipmentBehaviorFixture f, Dictionary<string, byte[]> expected)
    {
        var actual = ProcFiles(f); Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
        foreach (var pair in expected) Assert.Equal(pair.Value, actual[pair.Key]);
    }
}
