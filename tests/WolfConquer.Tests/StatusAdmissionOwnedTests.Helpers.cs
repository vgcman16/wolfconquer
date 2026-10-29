using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class StatusAdmissionOwnedTests
{
    private const uint Actor = OwnedEquipmentBehaviorFixture.Actor, Peer = OwnedEquipmentBehaviorFixture.Peer;
    private static bool Flag(CharacterSnapshot character, int flag) => LegacyPlayerDeathState.HasStatusFlag(character.Attributes, flag);
    private static Task Cast(OwnedEquipmentBehaviorFixture f, ushort id) => f.SendAsync(Actor,
        new MsgAttackPacket(new MsgAttackQuery(Actor, Actor, 832, 587, MsgAttackType.Magic,
            0, 0, 0, MsgAttackEffect.None, 0, SpellId: id)).ToFrameBytes());
    private static LegacyOrdinarySpellBinding Owner(OwnedEquipmentBehaviorFixture f)
    {
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Current().NativeGameClient!.OrdinarySpellBinding);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(binding.IsRetired);
        Assert.False(f.Recovery.IsBlocked); return binding;
    }
    private static void Unchanged(OwnedEquipmentBehaviorFixture f, WorldSession actor, WorldSession peer,
        Dictionary<string, byte[]> files, int actorFrames, int peerFrames, int failureMessages = 0)
    {
        Assert.Same(actor, f.Current()); Assert.Same(peer, f.Current(Peer));
        Assert.Same(actor.NativeGameClient!.OrdinarySpellBinding, Owner(f));
        Assert.Equal(500L, actor.Character.MinMana); Assert.Equal(500U, actor.NativePlayer!.Mana);
        Assert.Equal(500L, f.Data.Loader.LoadByAccountId(Actor)!.MinMana);
        Assert.Equal("100", actor.Character.Attributes["Stamina"]);
        Assert.Null(actor.MagicCooldowns); Assert.Equal((ushort)0, actor.LegacyRandomSpell);
        Assert.Equal(actorFrames + failureMessages, f.ActorFrames.Count); Assert.Equal(peerFrames, f.Frames(Peer).Count);
        Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(f.Connection(Actor), actor));
        f.Data.AssertFiles(files); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        if (failureMessages != 0)
        {
            var bytes = Assert.Single(f.ActorFrames.Skip(actorFrames));
            Assert.True(LegacyMsgMessagePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var message));
            Assert.Equal("You can't use this skill right now!", message.Content[3]);
        }
    }
    private static LegacySpellAnimationPacket[] Animations(OwnedEquipmentBehaviorFixture f) => f.ActorFrames
        .Select(OwnedEquipmentBehaviorFixture.ReadFrame).Where(frame => LegacySpellAnimationPacket.TryParse(frame, out _))
        .Select(frame => { Assert.True(LegacySpellAnimationPacket.TryParse(frame, out var packet)); return packet; }).ToArray();
    private static async Task InvokeRetained(OwnedEquipmentBehaviorFixture f, WorldSession caller, ushort id)
    {
        var context = f.Handler.CreateMonsterRuntimeContext(new(f.Connection(Actor), caller));
        var world = Assert.IsType<WorldDataCatalog>(context.WorldData);
        var learned = Assert.Single(caller.PlayerSpells.Spells, row => row.Id == id);
        Assert.True(LegacyMagicRuntime.TryResolveNativeLoadedSpell(world, id, learned.Level, out var spell, out _));
        var operations = Assert.IsType<WorldOperationBarrier>(typeof(WorldServerFrameHandler)
            .GetField("_worldOperations", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler));
        var method = typeof(WorldPacketHandlers).GetMethod("HandleLegacyPlayerAttachStatusMagicAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        var query = new MsgAttackQuery(Actor, Actor, 832, 587, MsgAttackType.Magic, 0, 0, 0, MsgAttackEffect.None, 0, SpellId: id);
        using var shared = await operations.EnterSharedAsync();
        object? pending;
        try { pending = method.Invoke(null, [context, query, learned, spell, CancellationToken.None]); }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        { ExceptionDispatchInfo.Capture(inner).Throw(); throw; }
        Assert.True(await Assert.IsType<ValueTask<bool>>(pending));
    }
}
