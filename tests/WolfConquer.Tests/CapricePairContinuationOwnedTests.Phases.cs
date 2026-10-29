using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class CapricePairContinuationOwnedTests
{
    private const uint Actor = OwnedEquipmentBehaviorFixture.Actor, Peer = OwnedEquipmentBehaviorFixture.Peer;
    private static object Invoke(string method, params object?[] arguments)
    {
        try { return typeof(WorldPacketHandlers).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, arguments)!; }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        { ExceptionDispatchInfo.Capture(inner).Throw(); throw; }
    }
    private static object Member(object value, string name) => value.GetType()
        .GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(value)!;
    private static async Task<object> AwaitPrivateValue(object value)
    {
        var task = Assert.IsAssignableFrom<Task>(value.GetType().GetMethod("AsTask")!.Invoke(value, null));
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task)!;
    }
    private static WorldOperationBarrier Operations(OwnedEquipmentBehaviorFixture f) => Assert.IsType<WorldOperationBarrier>(
        typeof(WorldServerFrameHandler).GetField("_worldOperations", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
    private static LegacyOrdinarySpellBinding Binding(OwnedEquipmentBehaviorFixture f, uint uid)
    {
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Current(uid).NativeGameClient!.OrdinarySpellBinding);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(binding.IsRetired);
        Assert.Same(f.Current(uid).NativePlayer, binding.Player); return binding;
    }
    private static CharacterSnapshot Saved(OwnedEquipmentBehaviorFixture f, uint uid) => Assert.IsType<CharacterSnapshot>(
        f.Data.Loader.TryLoadByAccountIdForAdmission(uid).Snapshot);
    private static bool Stamina(byte[] bytes, ulong amount) =>
        LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var update) &&
        update.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.Stamina && row.Value == amount);
    private static void DurablePair(OwnedEquipmentBehaviorFixture f)
    {
        Assert.Equal("60", Saved(f, Actor).Attributes["Stamina"]);
        Assert.Equal("60", Saved(f, Peer).Attributes["Stamina"]);
        Assert.Equal(493L, Saved(f, Actor).MinMana);
        Assert.Equal(493L, f.Current(Actor).Character.MinMana);
        Assert.Equal(493U, Binding(f, Actor).Player.Mana);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }
    private static async Task<(WorldPacketHandlerContext Context, object Prepared, object Result, object Receipt)>
        PrepareActualReceive(OwnedEquipmentBehaviorFixture f, WorldDataCatalog world)
    {
        var context = f.Handler.CreateMonsterRuntimeContext(new(f.Connection(Actor), f.Current(Actor)));
        Assert.Same(f.Store, context.Sessions); Assert.Same(f.Recovery, context.TradeRecovery);
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(f.Current(Actor).PlayerSpells, world, 1000, out _, out var spell));
        var admitted = await Assert.IsType<ValueTask<WorldSession?>>(Invoke("TryPrepareLegacyRootFirstFamilyCanUseAsync",
            context, f.Current(Actor), spell, CancellationToken.None, null));
        Assert.NotNull(admitted); Assert.Same(admitted, f.Current(Actor));
        Assert.Equal(493L, admitted.Character.MinMana); Assert.Equal(493L, Saved(f, Actor).MinMana);
        context = f.Handler.CreateMonsterRuntimeContext(new(f.Connection(Actor), admitted));
        // Actual early modifier phase and ACTUAL returned preparation. These are
        // component boundaries, not a fabricated completed receive or new hook.
        var early = await AwaitPrivateValue(Invoke("ApplyLegacyPlayerReceiveEarlyModifiersAsync", context,
            new WorldActiveSession(f.Connection(Peer), f.Current(Peer)), admitted.Character, 421U,
            CancellationToken.None, true));
        Assert.Equal("None", Member(early, "AbortReason").ToString());
        context = f.Handler.CreateMonsterRuntimeContext(new(f.Connection(Actor), f.Current(Actor)));
        var prepared = Invoke("PrepareLegacyPlayerMagicReceive", context, early);
        Assert.True(Assert.IsType<bool>(Member(prepared, "RequiresDamageCommit")));
        var result = Member(prepared, "Result");
        Assert.False(Assert.IsType<bool>(Member(result, "Killed")));
        Assert.Equal(421U, Assert.IsType<uint>(Member(result, "DamageApplied")));
        Assert.True(Assert.IsType<bool>(Member(result, "CapriceStaminaTransferred")));
        var receipt = Member(result, "CapriceReceipt"); Assert.NotNull(receipt);
        DurablePair(f);
        return (context, prepared, result, receipt);
    }
    private static async Task<bool> SendActualReceipt(object receipt)
    {
        object pending;
        try { pending = receipt.GetType().GetMethod("SendAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(receipt, [CancellationToken.None])!; }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        { ExceptionDispatchInfo.Capture(inner).Throw(); throw; }
        return await Assert.IsType<ValueTask<bool>>(pending);
    }
}
