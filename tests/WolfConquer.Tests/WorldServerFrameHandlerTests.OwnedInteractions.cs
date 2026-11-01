using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    private const uint LegacyInteractionMover = 1_293_600, LegacyInteractionPeer = 1_293_601, LegacyInteractionWatcher = 1_293_602;

    private static Task WithOwnedLegacyInteractionsAsync(WorldDataCatalog catalog,
        Func<OwnedLegacyInteractionContext, Task> body, bool watcher = false, bool namedPartner = false)
    {
        var actor = LegacyInteractionInput(LegacyInteractionMover, "HeroOne", 830);
        var peer = LegacyInteractionInput(LegacyInteractionPeer, namedPartner ? "Partner" : "HeroOne", 830);
        var others = watcher ? new[] { peer, LegacyInteractionInput(LegacyInteractionWatcher, "Watcher", 835) } : new[] { peer };
        return OwnedEquipmentBehaviorFixture.RunAsync(actor, catalog, async owner =>
        {
            var run = new OwnedLegacyInteractionContext(owner, watcher);
            run.Clear(); await body(run);
        }, others);
    }

    private static OwnedEquipmentBehaviorInput LegacyInteractionInput(uint uid, string name, long x)
    {
        // Retain the old declared class7051/level140/reborn2/agility36/body/
        // wallets/HP61258 saved inputs. Real owned startup may legitimately
        // clamp HP before this test's baseline; it is never hand-rebased.
        var character = OwnedEquipmentBehaviorFixture.Character(uid, name, mapId: 1015, x: x, y: 584);
        character = character with { Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MaxMana"] = character.MinMana.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ExpBallUsed"] = "0", ["WHMoney"] = "0", ["LegacyInteractionOpaque"] = "retain",
            ["EpicTrojan"] = "0/0/0/0/1/1/0/0/0/0/0/0/0/0/1/0/0/0/1/0/",
        } };
        return new(character, PlayerItemSnapshot.Empty);
    }

    private sealed class OwnedLegacyInteractionContext
    {
        internal readonly OwnedEquipmentBehaviorFixture Owner;
        internal readonly uint[] Uids;
        internal readonly Dictionary<uint, CharacterSnapshot> Initial;
        internal readonly List<(uint Recipient, byte[] Bytes)> Order = [];
        internal Action<uint, byte[]>? Observe;
        internal OwnedLegacyInteractionContext(OwnedEquipmentBehaviorFixture owner, bool watcher)
        {
            Owner = owner; Uids = watcher ? [LegacyInteractionMover, LegacyInteractionPeer, LegacyInteractionWatcher] : [LegacyInteractionMover, LegacyInteractionPeer];
            Initial = Uids.ToDictionary(uid => uid, uid => owner.Current(uid).Character);
            owner.BeforeSend = (uid, bytes) => { Observe?.Invoke(uid, bytes); Order.Add((uid, bytes)); };
            AssertOwnedLegacyInteractionState(this);
        }
        internal WorldSession Current(uint uid = LegacyInteractionMover) => Owner.Current(uid);
        internal List<byte[]> Frames(uint uid) => Owner.Frames(uid);
        internal Task Send(uint uid, byte[] bytes) => Owner.SendAsync(uid, bytes);
        internal void Clear() { Observe = null; Order.Clear(); foreach (uint uid in Uids) Frames(uid).Clear(); }
    }

    private static async Task PairOwnedLegacyInteraction(OwnedLegacyInteractionContext run, Dictionary<string, byte[]> files)
    {
        run.Observe = (_, bytes) =>
        {
            run.Owner.Data.AssertFiles(files); AssertOwnedLegacyInteractionState(run);
            Assert.True(MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet));
            if (packet.Query.AttackType == MsgAttackType.InteractionRequest)
            { Assert.Null(run.Current().InteractionPartnerUid); Assert.Null(run.Current(LegacyInteractionPeer).InteractionPartnerUid); }
            else
            { Assert.Equal(LegacyInteractionPeer, run.Current().InteractionPartnerUid); Assert.Equal(LegacyInteractionMover, run.Current(LegacyInteractionPeer).InteractionPartnerUid); }
            Assert.Equal(7U, run.Current().ActiveInteractionDance); Assert.Equal(7U, run.Current(LegacyInteractionPeer).ActiveInteractionDance);
        };
        // Retain the old exact request/accept input metadata helper while
        // routing it through this real owned D13 Frame, not its old D11 owner.
        await PairInteractionAsync(run.Owner.Handler, run.Owner.Connection(LegacyInteractionMover), run.Owner.Connection(LegacyInteractionPeer));
        Assert.Equal(new[] { LegacyInteractionPeer, LegacyInteractionPeer, LegacyInteractionMover, LegacyInteractionPeer }, run.Order.Select(row => row.Recipient));
        Assert.All(run.Order.Take(2), row => Assert.Equal(LegacyInteractionAttackGolden(46, LegacyInteractionMover, LegacyInteractionPeer, 0, 7), row.Bytes));
        Assert.True(MsgAttackPacket.TryParse(ReadFrame(run.Order[0].Bytes), out var request));
        Assert.Equal(MsgAttackType.InteractionRequest, request.Query.AttackType); Assert.Equal(7U, request.Query.ResponseDamage);
        Assert.All(run.Order.Skip(2), row => Assert.Equal(LegacyInteractionAttackGolden(47, LegacyInteractionPeer, LegacyInteractionMover, 103, 7), row.Bytes));
        Assert.True(MsgAttackPacket.TryParse(ReadFrame(run.Order[2].Bytes), out var accept));
        Assert.Equal(MsgAttackType.InteractionAccept, accept.Query.AttackType); Assert.Equal(7U, accept.Query.ResponseDamage);
        run.Clear();
    }

    private static byte[] LegacyInteractionPacket(MsgAttackType kind, uint actor, uint other, int damage, uint dance) =>
        new MsgAttackPacket(new(actor, other, 830, 584, kind, damage, 0, dance, MsgAttackEffect.None, 0)).ToFrameBytes();
}
