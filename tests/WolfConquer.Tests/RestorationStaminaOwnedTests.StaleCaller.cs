using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyMagicAttackRuntimeTests;

namespace WolfConquer.Tests;

public sealed partial class RestorationStaminaOwnedTests
{
    // Existing16-word,32-bit-index input format; flag0xC0 is bit0 of word6.
    private const string RestorationStaminaOmnipotence = "0/0/0/0/0/0/1/0/0/0/0/0/0/0/0/0";

    [Theory]
    [InlineData((ushort)11960, false)]
    [InlineData((ushort)12890, true)]
    [InlineData((ushort)12890, false)]
    public Task RestorationStaminaActualAdmissionOwnerRefusesInvalidCurrentDespiteValidRetainedCaller(
        ushort id, bool callerOmnipotence) => F.RestorationStaminaRun(id, "93", false, callerOmnipotence, 0, 7, async f =>
        {
            var caller = f.Current(); var binding = Owner(f);
            RestorationStaminaPublishCurrent(f, "not-a-number", id == 12890 ? true : null);
            var expected = f.Current(); Assert.NotSame(caller, expected);
            Assert.Equal("93", caller.Character.Attributes["Stamina"]);
            Assert.Equal("not-a-number", expected.Character.Attributes["Stamina"]);
            if (id == 12890 && !callerOmnipotence)
            {
                Assert.False(caller.Character.Attributes.ContainsKey("StatusFlags"));
                Assert.Equal(RestorationStaminaOmnipotence, expected.Character.Attributes["StatusFlags"]);
            }
            var files = f.Data.Files(); var start = f.ActorFrames.Count;

            Assert.True(await RestorationStaminaInvokeRetainedHandler(f, caller, id));

            Assert.Same(expected, f.Current()); Assert.Same(binding, Owner(f));
            Assert.Equal(500L, f.Current().Character.MinMana); Assert.Equal(500U, binding.Player.Mana);
            Assert.Equal(500L, Saved(f).MinMana); Assert.Null(f.Current().MagicCooldowns);
            Assert.Equal(start, f.ActorFrames.Count); f.Data.AssertFiles(files);
            Assert.Equal(0, Assert.Single(f.Current().PlayerSpells.Spells, row => row.Id == id).Experience);
            Assert.Contains("requiredStaminaInvalidBeforeAdmission", f.Log.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("restoration after accepted admission", f.Log.ToString(), StringComparison.Ordinal);
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        });

    [Theory]
    [InlineData((ushort)11960)] [InlineData((ushort)12890)]
    public Task RestorationStaminaValidCurrentOwnerIsNotVetoedByAnInvalidRetainedCaller(ushort id) =>
        F.RestorationStaminaRun(id, "not-a-number", false, id == 12890, 0, 7, async f =>
        {
            var caller = f.Current(); RestorationStaminaPublishCurrent(f, "93", null);
            var start = f.ActorFrames.Count;

            Assert.True(await RestorationStaminaInvokeRetainedHandler(f, caller, id));

            var expected = id == 11960 ? 100UL : 103UL;
            Assert.Equal(expected.ToString(System.Globalization.CultureInfo.InvariantCulture), f.Current().Character.Attributes["Stamina"]);
            Assert.Equal("not-a-number", caller.Character.Attributes["Stamina"]);
            Assert.Equal(493L, f.Current().Character.MinMana); Assert.Equal(493L, Saved(f).MinMana); Owner(f);
            Assert.Equal(new[] { (LegacyMsgUpdateDataType.Mana, 493UL), (LegacyMsgUpdateDataType.Stamina, expected) }, Resources(f, start));
            Assert.Single(f.ActorFrames.Skip(start), bytes => LegacySpellAnimationPacket.TryParse(
                OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var animation) && animation.SpellId == id);
            Assert.DoesNotContain("requiredStaminaInvalidBeforeAdmission", f.Log.ToString(), StringComparison.Ordinal);
        });

    [Fact]
    public Task RestorationStaminaCurrentOmnipotenceRemovalKeepsUnusedMalformedStateOptional() =>
        F.RestorationStaminaRun(12890, "not-a-number", false, true, 0, 7, async f =>
        {
            var caller = f.Current(); Assert.Equal(RestorationStaminaOmnipotence, caller.Character.Attributes["StatusFlags"]);
            RestorationStaminaPublishCurrent(f, "not-a-number", false);
            Assert.False(f.Current().Character.Attributes.ContainsKey("StatusFlags"));
            var start = f.ActorFrames.Count;

            Assert.True(await RestorationStaminaInvokeRetainedHandler(f, caller, 12890));

            Assert.Equal("not-a-number", f.Current().Character.Attributes["Stamina"]);
            Assert.Equal(493L, f.Current().Character.MinMana); Assert.Equal(493L, Saved(f).MinMana); Owner(f);
            Assert.Equal(new[] { (LegacyMsgUpdateDataType.Mana, 493UL) }, Resources(f, start));
            Assert.Single(f.ActorFrames.Skip(start), bytes => LegacySpellAnimationPacket.TryParse(
                OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var animation) && animation.SpellId == 12890);
            Assert.DoesNotContain("requiredStaminaInvalidBeforeAdmission", f.Log.ToString(), StringComparison.Ordinal);
        });

    private static void RestorationStaminaPublishCurrent(OwnedEquipmentBehaviorFixture f, string raw, bool? omnipotence)
    {
        var current = f.Current();
        var attributes = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["Stamina"] = raw };
        if (omnipotence == true) attributes["StatusFlags"] = RestorationStaminaOmnipotence;
        else if (omnipotence == false) attributes.Remove("StatusFlags");
        f.Store.Set(f.Connection(OwnedEquipmentBehaviorFixture.Actor), current with
            { Character = current.Character with { Attributes = attributes } });
        Owner(f); // Actual independently accepted owner, no fake saver/pin.
    }

    private static async Task<bool> RestorationStaminaInvokeRetainedHandler(OwnedEquipmentBehaviorFixture f,
        WorldSession caller, ushort id)
    {
        T Actual<T>(string field) where T : class => Assert.IsType<T>(typeof(WorldServerFrameHandler)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
        var world = Actual<WorldDataCatalog>("_worldData");
        var operations = Actual<WorldOperationBarrier>("_worldOperations");
        var learned = Assert.Single(caller.PlayerSpells.Spells, row => row.Id == id);
        Assert.True(LegacyMagicRuntime.TryResolveNativeLoadedSpell(world, id, learned.Level, out var spell, out _));
        var context = new WorldPacketHandlerContext(f.Connection(OwnedEquipmentBehaviorFixture.Actor), f.Log, caller,
            WorldData: world, Sessions: f.Store, PlayerSpellsRoot: f.Data.Spells,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13)
            { Trades = Actual<LegacyTradeStateStore>("_tradeStates"), TradeOperations = operations, TradeRecovery = f.Recovery };
        var query = new MsgAttackQuery(OwnedEquipmentBehaviorFixture.Actor, 0, 842, 584,
            MsgAttackType.Magic, 0, 0, 0, MsgAttackEffect.None, 0, SpellId: id);
        var method = typeof(WorldPacketHandlers).GetMethod(id == 11960 ? "HandleLegacyPlayerBreathFocusMagicAsync" :
            "HandleLegacyPlayerSwirlingStormMagicAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        using var operation = await operations.EnterSharedAsync();
        object? result;
        try { result = method.Invoke(null, [context, caller, query, learned, spell, CancellationToken.None]); }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        { ExceptionDispatchInfo.Capture(inner).Throw(); throw; }
        return result switch
        {
            ValueTask<bool> pending => await pending,
            Task<bool> pending => await pending,
            _ => throw new InvalidOperationException("Unexpected actual restoration-handler return type.")
        };
    }
}
