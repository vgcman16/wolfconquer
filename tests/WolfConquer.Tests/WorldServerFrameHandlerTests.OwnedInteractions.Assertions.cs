using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    private static void AssertOwnedLegacyInteractionState(OwnedLegacyInteractionContext run)
    {
        foreach (uint uid in run.Uids)
        {
            var current = run.Current(uid); var player = current.NativeGameClient!.Player;
            var binding = current.NativeGameClient.OrdinarySpellBinding!;
            Assert.True(run.Owner.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Equal(current.Character.Uid, (long)player.UID); Assert.Equal(current.Character.Reborn, (long)player.Reborn);
            Assert.Equal(current.Character.Class, (long)player.Class); Assert.Equal(current.Character.Level, (long)player.Level);
            Assert.Equal(current.Character.Agility, (long)player.Agility); Assert.Equal(current.Character.MapId, (long)player.Map);
            Assert.Equal(current.Character.X, (long)player.X); Assert.Equal(current.Character.Y, (long)player.Y);
            Assert.Equal(0U, player.DynamicID); Assert.Equal(LegacyActionState.RequireCurrent(current.Character), (uint)player.Action);
            Assert.Equal(current.Character.MinHitPoints, (long)player.HitPoints); Assert.Equal(current.Character.MinMana, (long)player.Mana);
            Assert.Equal(LegacyStaminaState.RequireCurrent(current.Character), player.Stamina);
            Assert.Equal(current.Character.Money, player.Money); Assert.Equal(current.Character.ConquerPoints, (long)player.ConquerPoints);
            Assert.Equal(0L, player.WHMoney); Assert.False(current.NativeGameClient.OnAutoAttack); Assert.False(current.NativeGameClient.FullLoading);
            var before = run.Initial[uid];
            Assert.True(WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(before with { X = current.Character.X, Y = current.Character.Y }, current.Character));
            Assert.True(WorldPacketHandlers.BoundHitpointsInputs.PaidEqual(before, current.Character));
            Assert.Equal(LegacyStaminaState.RequireCurrent(before), LegacyStaminaState.RequireCurrent(current.Character));
            Assert.Equal("retain", current.Character.Attributes["LegacyInteractionOpaque"]);
            var items = PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(File.ReadAllBytes(run.Owner.ItemPath(uid))));
            Assert.Equal(items, PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
            Assert.Equal(items, PlayerItemSnapshotLoader.ToLegacyBytes(binding.Items.Image.FinalSnapshot));
            Assert.Empty(current.PlayerSpells.Spells); Assert.Empty(current.PlayerProficiencies.Proficiencies);
        }
        Assert.False(LegacyTradeJournal.HasPending(run.Owner.Data.Users));
    }

    private static Dictionary<string, byte[]> OwnedLegacyInteractionMovedFiles(OwnedLegacyInteractionContext run,
        Dictionary<string, byte[]> before, int x, int y, bool paired = true)
    {
        var result = before.ToDictionary(row => row.Key, row => row.Value, StringComparer.OrdinalIgnoreCase);
        foreach (uint uid in paired ? new[] { LegacyInteractionMover, LegacyInteractionPeer } : new[] { LegacyInteractionMover })
        {
            string key = Path.GetRelativePath(run.Owner.Data.Root, run.Owner.CharacterPath(uid));
            string text = CharacterSnapshotFileEncoding.Decode(result[key], out var encoding);
            string oldX = "\r\nX=830\r\n", oldY = "\r\nY=584\r\n";
            Assert.Equal(2, text.Split(oldX, StringSplitOptions.None).Length); Assert.Equal(2, text.Split(oldY, StringSplitOptions.None).Length);
            text = text.Replace(oldX, "\r\nX=" + x.ToString(CultureInfo.InvariantCulture) + "\r\n", StringComparison.Ordinal)
                .Replace(oldY, "\r\nY=" + y.ToString(CultureInfo.InvariantCulture) + "\r\n", StringComparison.Ordinal);
            result[key] = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        }
        return result;
    }

    // Independent fixed2141 layout. Static codec/parser evidence is retained
    // separately from genuine owned D13 dispatch; no D11 owner is invented.
    private static byte[] OwnedLegacyInteractionWalkGolden(bool jump)
    {
        var bytes = new byte[24]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, 24); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2141);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), jump ? (ushort)2 : (ushort)1);
        if (jump) { BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 840); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 590); }
        else bytes[6] = 5;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), LegacyInteractionMover);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(20), LegacyInteractionPeer); return bytes;
    }
    private static byte[] LegacyInteractionAttackGolden(uint kind, uint actor, uint peer, uint damage, uint dance) =>
        OwnedLegacyInteractionProto(2135, [(4, actor), (5, peer), (7, 830), (8, 584), (13, kind), (15, damage), (17, 0), (21, dance), (25, 0), (26, 0)]);
    private static byte[] OwnedLegacyInteractionNpcGolden(uint uid, uint mesh, uint type) =>
        OwnedLegacyInteractionProto(2210, [(1, uid), (2, uid), (3, 0), (4, 831), (5, 583), (6, mesh), (7, type), (8, 0)]);
    private static byte[] OwnedLegacyInteractionGateGolden(bool jump)
    {
        var bytes = OwnedLegacyInteractionProto(2166, [(1, 25005), (2, 0), (3, 0), (4, 50000), (5, 50000),
            (6, jump ? 840UL : 831UL), (7, jump ? 590UL : 583UL), (8, 1137), (9, 21), (10, 17)]).ToList();
        bytes.Add(98); bytes.Add(4); bytes.AddRange(Encoding.UTF8.GetBytes("Gate"));
        var result = bytes.ToArray(); BinaryPrimitives.WriteUInt16LittleEndian(result, checked((ushort)result.Length)); return result;
    }
    private static byte[] OwnedLegacyInteractionProto(ushort opcode, (int Field, ulong Value)[] fields)
    {
        var result = new List<byte> { 0, 0, (byte)opcode, (byte)(opcode >> 8) };
        foreach (var field in fields) { OwnedLegacyInteractionVarint(result, checked((ulong)field.Field << 3)); OwnedLegacyInteractionVarint(result, field.Value); }
        var bytes = result.ToArray(); BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)bytes.Length)); return bytes;
    }
    private static void OwnedLegacyInteractionVarint(List<byte> bytes, ulong value)
    { do { byte part = (byte)(value & 127); value >>= 7; bytes.Add((byte)(part | (value == 0 ? 0 : 128))); } while (value != 0); }
}
