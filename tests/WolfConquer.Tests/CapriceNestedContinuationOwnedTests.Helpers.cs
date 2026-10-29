using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class CapriceNestedContinuationOwnedTests
{
    private const uint Actor = OwnedEquipmentBehaviorFixture.Actor, Peer = OwnedEquipmentBehaviorFixture.Peer;
    private static CharacterSnapshot Saved(OwnedEquipmentBehaviorFixture f, uint uid) => Assert.IsType<CharacterSnapshot>(
        f.Data.Loader.TryLoadByAccountIdForAdmission(uid).Snapshot);
    private static LegacyOrdinarySpellBinding Binding(OwnedEquipmentBehaviorFixture f, uint uid)
    {
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Current(uid).NativeGameClient!.OrdinarySpellBinding);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(binding.IsRetired); return binding;
    }
    private static WorldOperationBarrier Operations(OwnedEquipmentBehaviorFixture f) => Assert.IsType<WorldOperationBarrier>(
        typeof(WorldServerFrameHandler).GetField("_worldOperations", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
    private static async Task PrepareQueries(OwnedEquipmentBehaviorFixture f)
    {
        foreach (var uid in new[] { Actor, Peer })
            await f.SendAsync(uid, new MsgActionPacket { ObjId = uid, Type = (MsgActionType)251 }.ToFrameBytes());
        Assert.Equal(3000L, f.Current(Actor).Character.MinHitPoints);
        Assert.Equal(1L, f.Current(Peer).Character.MinHitPoints);
        Assert.Equal(3000, Binding(f, Actor).Player.HitPoints); Assert.Equal(1, Binding(f, Peer).Player.HitPoints);
        Assert.Equal("2", f.Current(Actor).Character.Attributes["TransformationID"]);
        Assert.Equal("2", f.Current(Peer).Character.Attributes["TransformationID"]);
        Assert.Equal(500L, Saved(f, Actor).MinMana);
        f.ActorFrames.Clear(); f.Frames(Peer).Clear();
    }
    private static async Task<object> InvokeWrapper(OwnedEquipmentBehaviorFixture f, ushort proc)
    {
        var context = f.Handler.CreateMonsterRuntimeContext(new(f.Connection(Actor), f.Current(Actor)));
        Assert.Same(f.Store, context.Sessions); Assert.Same(f.Recovery, context.TradeRecovery);
        var name = proc == 10950 ? "SendLegacyCombatGearStoneCrackerStarFlowSpellAsync" :
            "TrySendLegacyCombatGearUpdateTargetPlayerDamageSpellAsync";
        var count = proc == 10950 ? 5 : 7;
        var method = Assert.Single(typeof(WorldPacketHandlers).GetMethods(BindingFlags.Static | BindingFlags.NonPublic),
            candidate => candidate.Name == name && candidate.GetParameters().Length == count);
        object?[] arguments = proc == 10950
            ? [context, f.Current(Actor), (ushort)830, (ushort)584, CancellationToken.None]
            : [context, f.Connection(Peer), f.Current(Peer), f.Current(Actor), proc, 0, CancellationToken.None];
        object pending;
        try { pending = method.Invoke(null, arguments)!; }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        { ExceptionDispatchInfo.Capture(inner).Throw(); throw; }
        var task = Assert.IsAssignableFrom<Task>(pending.GetType().GetMethod("AsTask")!.Invoke(pending, null));
        await task;
        return task.GetType().GetProperty("Result")!.GetValue(task)!;
    }
    private static WorldSession Historical(Exception error, string property) => Assert.IsType<WorldSession>(error.GetType()
        .GetProperty(property, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(error));
    private static void PairSaved(OwnedEquipmentBehaviorFixture f)
    {
        Assert.Equal("60", Saved(f, Actor).Attributes["Stamina"]);
        Assert.Equal("60", Saved(f, Peer).Attributes["Stamina"]);
        Assert.Equal(493L, Saved(f, Actor).MinMana);
        Assert.Equal(493L, f.Current(Actor).Character.MinMana);
        Assert.Equal(493U, Binding(f, Actor).Player.Mana);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }
    private static bool Finish(byte[] bytes, uint uid) => MsgActionPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var action) &&
        action.Type == MsgActionType.FinishTransform && action.ObjId == uid;
    private static bool Spell(byte[] bytes, ushort spell) => LegacySpellAnimationPacket.TryParse(
        OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var animation) && animation.SpellId == spell;
    private static bool Stamina(byte[] bytes, ulong value) => LegacyMsgUpdatePacket.TryParse(
        OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var update) &&
        update.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.Stamina && row.Value == value);
}
