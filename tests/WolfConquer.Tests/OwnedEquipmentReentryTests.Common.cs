using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class OwnedEquipmentReentryTests
{
    private const uint Actor = OwnedEquipmentBehaviorFixture.Actor, Target = OwnedEquipmentBehaviorFixture.Peer;
    private static byte[] Attack(uint actor, uint target) => new MsgAttackPacket(new MsgAttackQuery(
        Uid: actor, OpponentUid: target, X: 120, Y: 121, AttackType: MsgAttackType.Physical,
        Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes();
    private static async Task<LegacyPlayerStatusPacket> Status(OwnedEquipmentBehaviorFixture f, uint uid)
    {
        var start = f.ActorFrames.Count;
        await f.SendAsync(Actor, new LegacyPlayerStatusQueryPacket(uid).ToFrameBytes());
        var bytes = Assert.Single(f.ActorFrames.Skip(start), value => LegacyPlayerStatusPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(value), out _));
        Assert.True(LegacyPlayerStatusPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var status));
        return status;
    }
    private static LegacySpellAnimationPacket Animation(IEnumerable<byte[]> frames, ushort id) => Assert.Single(
        frames.Select(bytes => LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) ? packet : null)
            .Where(packet => packet is not null && packet.SpellId == id).Select(packet => packet!));
    private static bool Flag(WorldSession session, int flag)
    {
        if (!session.Character.Attributes.TryGetValue("StatusFlags", out var text)) return false;
        var words = text.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length > flag / 32 && (ulong.Parse(words[flag / 32], CultureInfo.InvariantCulture) & (1UL << (flag % 32))) != 0;
    }
}
