using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlFloorDropTests
{
    private static Dictionary<string, byte[]> ExpectedFiles(FloorFixture f, bool reward)
    {
        var files = f.Owner.Data.Files();
        string key = Path.GetRelativePath(f.Owner.Data.Root, f.Owner.CharacterPath());
        string text = Encoding.UTF8.GetString(files[key]);
        const string beforeMana = "\r\nMinMana=500\r\n", afterMana = "\r\nMinMana=493\r\n";
        Assert.Contains(beforeMana, text);
        text = text.Replace(beforeMana, afterMana, StringComparison.Ordinal);
        if (reward)
        {
            const string beforeCp = "\r\nConquerPoints=639778904\r\n", afterCp = "\r\nConquerPoints=639778909\r\n";
            Assert.Contains(beforeCp, text); text = text.Replace(beforeCp, afterCp, StringComparison.Ordinal);
        }
        files[key] = Encoding.UTF8.GetBytes(text);
        return files;
    }

    private static void AssertAcceptedDeathPrefix(FloorFixture f)
    {
        Assert.True(f.Binding.Player.Mana == 493U, $"Expected committed mana493; actual={f.Binding.Player.Mana}.\n{f.Owner.Log}"); Assert.Equal(95, (int)f.Binding.Player.Stamina);
        Assert.Equal(51_000, f.Binding.Player.HitPoints);
        Assert.Equal(f.Clock.GetLocalNow().DateTime, f.Binding.Player.AttackStamp);
        Assert.False(f.Monsters.TryGetVisibleInstance(f.Monster, f.Clock.GetUtcNow(), out _));
        var deathBytes = Assert.Single(f.Owner.ActorFrames, bytes => MsgAttackPacket.TryParse(
            OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) && packet.Query.AttackType == MsgAttackType.Death);
        Assert.Equal((ushort)2135, OwnedEquipmentBehaviorFixture.ReadFrame(deathBytes).Header.Opcode);
        Assert.True(MsgAttackPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(deathBytes), out var death));
        Assert.Equal(Actor, death.Query.Uid); Assert.Equal(f.Monster.Uid, death.Query.OpponentUid);
        Assert.Equal((ushort)11, death.Query.X); Assert.Equal((ushort)10, death.Query.Y);
        Assert.Equal(1, death.Query.KilledMonster); Assert.Equal(0, death.Query.Damage);
        Assert.DoesNotContain(f.Owner.ActorFrames, bytes => MsgAttackPacket.TryParse(
            OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) && packet.Query.AttackType == MsgAttackType.Physical);
    }

    private static void AssertCompleted(FloorFixture f)
    {
        AssertAcceptedDeathPrefix(f);
        Assert.True(f.Owner.Store.TryGet(f.Owner.Connection(Actor), out var current), f.Owner.Log.ToString());
        Assert.Same(f.Binding, current.NativeGameClient!.OrdinarySpellBinding);
        Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(f.Binding)); Assert.False(f.Binding.IsRetired);
        Assert.Equal(493L, current.Character.MinMana); Assert.Equal(639_778_909L, current.Character.ConquerPoints);
        Assert.Equal(639_778_909U, f.Binding.Player.ConquerPoints);
        Assert.Equal(0L, current.Character.Experience);
        Assert.Equal(0, Assert.Single(current.PlayerSpells.Spells, row => row.Id == f.SpellId).Experience);
        Assert.False(f.Owner.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
        var bytes = Assert.Single(f.Owner.ActorFrames, frame => Animation(frame, f.SpellId));
        Assert.Equal((ushort)2239, OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode);
        Assert.True(LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var animation));
        Assert.Equal(Actor, animation.AttackerUid);
        // The real range4 Ironbone line ends at(14,10); Dragonhowl retains
        // its anchored target(11,10). Both keep the independent4200 damage.
        Assert.Equal(f.SpellId == 17_650 ? (ushort)14 : (ushort)11, animation.X);
        Assert.Equal((ushort)10, animation.Y);
        var hit = Assert.Single(animation.Targets);
        Assert.Equal(f.Monster.Uid, hit.Uid); Assert.Equal(4_200U, hit.Damage);
        Assert.True(hit.Hit); Assert.Equal(MsgAttackEffect.None, hit.Effect);
        int deathIndex = f.Owner.ActorFrames.FindIndex(frame => MsgAttackPacket.TryParse(
            OwnedEquipmentBehaviorFixture.ReadFrame(frame), out var packet) && packet.Query.AttackType == MsgAttackType.Death);
        int cpIndex = f.Owner.ActorFrames.FindIndex(frame => IsCps(frame, 639_778_909));
        Assert.True(cpIndex > deathIndex); Assert.True(f.Owner.ActorFrames.IndexOf(bytes) > cpIndex);
    }

    private static void AssertFenced(FloorFixture f)
    {
        AssertAcceptedDeathPrefix(f);
        Assert.True(f.Owner.Recovery.IsBlocked);
        Assert.False(f.Owner.Store.TryGet(f.Owner.Connection(Actor), out _));
        Assert.True(f.Binding.IsRetired); Assert.False(f.Binding.Client.OnAutoAttack);
        Assert.Equal(639_778_904U, f.Binding.Player.ConquerPoints);
        Assert.DoesNotContain(f.Owner.ActorFrames, bytes => Animation(bytes, f.SpellId));
        Assert.DoesNotContain(f.Owner.ActorFrames, bytes => IsCps(bytes, 639_778_909));
        Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
    }

    private static bool Animation(byte[] bytes, ushort spellId) =>
        LegacySpellAnimationPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) && packet.SpellId == spellId;
    private static bool IsCps(byte[] bytes, ulong value) => LegacyMsgUpdatePacket.TryParse(
        OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) && packet.Uid == Actor &&
        packet.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.ConquerPoints && row.Value == value);
    private static LegacyFloorItemPacket[] FloorPackets(FloorFixture f) => f.Owner.ActorFrames.Select(bytes =>
        LegacyFloorItemPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) ? packet : null)
        .Where(packet => packet is not null).Cast<LegacyFloorItemPacket>().ToArray();

    // Independent ordinary2164 golden. Only a real pre-observed UID and fixed
    // expected tile/item vary; no producer serializer contributes expected bytes.
    private static byte[] FloorGolden(uint uid, uint item, ushort x, ushort y)
    {
        var bytes = new byte[153];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 153);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2164);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), uid);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), item);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), x);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), y);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 40);
        bytes[18] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24), Actor);
        return bytes;
    }
}
