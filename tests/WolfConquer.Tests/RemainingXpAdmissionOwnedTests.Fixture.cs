using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class RemainingXpAdmissionOwnedTests
{
    private const uint Actor = OwnedEquipmentBehaviorFixture.Actor, Peer = OwnedEquipmentBehaviorFixture.Peer;
    private static Task Cast(OwnedEquipmentBehaviorFixture f, ushort id) => f.SendAsync(Actor,
        new MsgAttackPacket(new MsgAttackQuery(Actor, Peer, 842, 584, MsgAttackType.Magic,
            0, 0, 0, MsgAttackEffect.None, 0, SpellId: id)).ToFrameBytes());
    private static bool Flag(CharacterSnapshot character, int flag) => LegacyPlayerDeathState.HasStatusFlag(character.Attributes, flag);
    private static LegacyOrdinarySpellBinding Owner(OwnedEquipmentBehaviorFixture f)
    {
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Current().NativeGameClient!.OrdinarySpellBinding);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(binding.IsRetired);
        Assert.False(f.Recovery.IsBlocked); return binding;
    }
    private static void Unchanged(OwnedEquipmentBehaviorFixture f, WorldSession actor, WorldSession peer,
        Dictionary<string, byte[]> files, int actorFrames, int peerFrames)
    {
        Assert.Same(actor, f.Current()); Assert.Same(peer, f.Current(Peer));
        Assert.Same(actor.NativeGameClient!.OrdinarySpellBinding, Owner(f));
        Assert.Equal(500L, actor.Character.MinMana); Assert.Equal(500U, actor.NativePlayer!.Mana);
        Assert.Equal(500L, f.Data.Loader.LoadByAccountId(Actor)!.MinMana);
        Assert.Equal(actorFrames, f.ActorFrames.Count); Assert.Equal(peerFrames, f.Frames(Peer).Count);
        Assert.Null(actor.MagicCooldowns); Assert.Null(peer.MagicCooldowns);
        Assert.Equal((ushort)0, actor.LegacyRandomSpell);
        Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(f.Connection(Actor), actor));
        f.Data.AssertFiles(files); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }
    private static (LegacyMsgUpdateDataType Type, ulong Value)[] Resources(OwnedEquipmentBehaviorFixture f) =>
        f.ActorFrames.Select(OwnedEquipmentBehaviorFixture.ReadFrame)
            .Where(frame => LegacyMsgUpdatePacket.TryParse(frame, out _))
            .SelectMany(frame => { Assert.True(LegacyMsgUpdatePacket.TryParse(frame, out var packet)); return packet.Updates; })
            .Where(row => row.Type is LegacyMsgUpdateDataType.Stamina or LegacyMsgUpdateDataType.Mana)
            .Select(row => (row.Type, row.Value)).ToArray();
    private static LegacySpellAnimationPacket[] Animations(OwnedEquipmentBehaviorFixture f) =>
        f.ActorFrames.Select(OwnedEquipmentBehaviorFixture.ReadFrame)
            .Where(frame => LegacySpellAnimationPacket.TryParse(frame, out _))
            .Select(frame => { Assert.True(LegacySpellAnimationPacket.TryParse(frame, out var animation)); return animation; }).ToArray();

    private static async Task InvokeRetained(OwnedEquipmentBehaviorFixture f, WorldSession caller, ushort id)
    {
        T Actual<T>(string name) where T : class => Assert.IsType<T>(typeof(WorldServerFrameHandler)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
        var world = Actual<WorldDataCatalog>("_worldData");
        var operations = Actual<WorldOperationBarrier>("_worldOperations");
        var learned = Assert.Single(caller.PlayerSpells.Spells, row => row.Id == id);
        Assert.True(LegacyMagicRuntime.TryResolveNativeLoadedSpell(world, id, learned.Level, out var spell, out _));
        var context = new WorldPacketHandlerContext(f.Connection(Actor), f.Log, caller, WorldData: world,
            Sessions: f.Store, PlayerSpellsRoot: f.Data.Spells, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13)
            { Trades = Actual<LegacyTradeStateStore>("_tradeStates"), TradeOperations = operations, TradeRecovery = f.Recovery };
        var query = new MsgAttackQuery(Actor, Peer, 842, 584, MsgAttackType.Magic, 0, 0, 0, MsgAttackEffect.None, 0, SpellId: id);
        var method = typeof(WorldPacketHandlers).GetMethod("HandleLegacyPlayerAttachStatusMagicAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        using var shared = await operations.EnterSharedAsync();
        object? result;
        try { result = method.Invoke(null, [context, query, learned, spell, CancellationToken.None]); }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        { ExceptionDispatchInfo.Capture(inner).Throw(); throw; }
        Assert.True(await Assert.IsType<ValueTask<bool>>(result));
    }
}
