using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // Raw saved inputs precede registry/bootstrap and genuine ordinary admission.
    // No fixture saver, callback authority or reconstructed native owner exists.
    private static Task ReceiveStaminaOwnedAsync(string? actorRaw, string? targetRaw, bool caprice,
        Func<OwnedEquipmentBehaviorFixture, WorldDataCatalog, Task> body,
        bool blessing = false, int branch = 1)
    {
        var world = CreateMagicCatalogWithItems(CreateMonsterFamily(), [],
            [CreateRetainedNativeMagic421Helmet(),
             OwnedEquipmentBehaviorFixture.Item(LegacyCapriceLevelTwentySevenItemId, "ReceiveStaminaCaprice")],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0));
        OwnedEquipmentBehaviorInput Input(uint uid, string? raw)
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = "0", ["PrestigeLevel"] = "5", ["LegacyConsolidationRoll"] = "99",
                ["LegacyCapriceRoll"] = "0", ["LegacyCapriceBranch"] = branch.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["LegacyCapriceStampUtc"] = "2026-01-01T00:00:00.0000000+00:00",
            };
            if (raw is not null) attributes["Stamina"] = raw;
            if (blessing) attributes["HeavenBlessing"] = "1";
            if (!caprice && uid == PlayerUid)
            { attributes["PerfectionStatus.LightOfStamina"] = "100"; attributes["LegacyLightOfStaminaRoll"] = "0"; }
            if (branch == 2 && uid == TargetPlayerUid) attributes["StatusFlags"] = BuildStatusFlags(LegacyXpListFlag);
            var items = caprice && uid == PlayerUid
                ? CreatePlayerItems(new PlayerItemSnapshotItem
                { Uid = LegacyCapriceLevelTwentySevenItemId, ItemId = LegacyCapriceLevelTwentySevenItemId, Position = 101 })
                : PlayerItemSnapshot.Empty;
            var input = CreateRetainedNativeMagic421Input(CreateSession(uid: uid,
                name: uid == PlayerUid ? "ReceiveStaminaActor" : "ReceiveStaminaPeer", x: uid == PlayerUid ? 830 : 832,
                y: 584, attributes: attributes, playerItems: items));
            return new(input.Character, input.PlayerItems, input.PlayerSpells);
        }
        var clock = new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
        return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(Input(PlayerUid, actorRaw), world,
            new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock),
            f => body(f, world), Input(TargetPlayerUid, targetRaw));
    }

    private static (bool Applied, bool Restored, CharacterSnapshot Actor, CharacterSnapshot Peer)
        ReceiveStaminaPrepare(OwnedEquipmentBehaviorFixture f, bool caprice)
    {
        var actor = f.Current(PlayerUid); var peer = f.Current(TargetPlayerUid);
        object?[] args = caprice
            ? [actor.Character, peer.Character, actor, peer, null, null]
            : [actor.Character, peer.Character, actor, null];
        object result;
        try
        {
            result = typeof(WorldPacketHandlers).GetMethod(caprice ? "TryApplyLegacyPlayerReceiveCaprice" :
                "TryApplyLegacyPlayerReceiveLightOfStamina", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args)!;
        }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        { ExceptionDispatchInfo.Capture(inner).Throw(); throw; }
        bool Flag(string name) => Assert.IsType<bool>(result.GetType().GetProperty(name)!.GetValue(result));
        return caprice
            ? (Flag("StaminaTransferred"), Flag("XpListRemoved"), Assert.IsType<CharacterSnapshot>(args[4]), Assert.IsType<CharacterSnapshot>(args[5]))
            : (Flag("Applied"), Flag("StaminaRestored"), Assert.IsType<CharacterSnapshot>(args[3]), peer.Character);
    }

    private static async Task ReceiveStaminaAttackAsync(OwnedEquipmentBehaviorFixture f, WorldDataCatalog world)
    {
        var context = new WorldPacketHandlerContext(f.Connection(PlayerUid), f.Log, f.Current(PlayerUid),
            WorldData: world, Sessions: f.Store, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        await PrepareRetainedNativeMagic421QueriesAsync(context, f.Connection(TargetPlayerUid),
            f.Frames(PlayerUid), f.Frames(TargetPlayerUid));
        var target = f.Current(TargetPlayerUid).Character;
        await f.SendAsync(PlayerUid, new MsgAttackPacket(new MsgAttackQuery(PlayerUid, TargetPlayerUid,
            checked((ushort)target.X), checked((ushort)target.Y), MsgAttackType.Magic, 0, 0, 0,
            MsgAttackEffect.None, 0, SpellId: ThunderSpellId)).ToFrameBytes());
    }

    private static void ReceiveStaminaUnchanged(OwnedEquipmentBehaviorFixture f, WorldSession actor, WorldSession peer,
        Dictionary<string, byte[]> files, int actorFrames, int peerFrames)
    {
        Assert.Same(actor, f.Current(PlayerUid)); Assert.Same(peer, f.Current(TargetPlayerUid));
        Assert.Equal(actorFrames, f.Frames(PlayerUid).Count); Assert.Equal(peerFrames, f.Frames(TargetPlayerUid).Count);
        Assert.Same(actor.NativeGameClient!.OrdinarySpellBinding, f.Current(PlayerUid).NativeGameClient!.OrdinarySpellBinding);
        Assert.False(actor.NativeGameClient.OrdinarySpellBinding!.IsRetired);
        Assert.False(peer.NativeGameClient!.OrdinarySpellBinding!.IsRetired);
        Assert.False(f.Recovery.IsBlocked); Assert.Null(actor.MagicCooldowns); Assert.Null(peer.MagicCooldowns);
        Assert.Equal(actor.Character.MinMana, actor.NativePlayer!.Mana);
        Assert.Equal(peer.Character.MinMana, peer.NativePlayer!.Mana);
        ProcFilesUnchanged(f, files);
    }
}
