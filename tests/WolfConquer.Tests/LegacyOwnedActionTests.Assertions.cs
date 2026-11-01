using System.Buffers.Binary;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedActionTests
{
    private static uint CurrentAction(ActionFixture f, uint uid = Uid) => uint.Parse(f.Get(uid).Character.Attributes["Action"], CultureInfo.InvariantCulture);
    private static ushort Stamina(ActionFixture f) => ushort.Parse(f.Current.Character.Attributes["Stamina"], CultureInfo.InvariantCulture);
    private static void ClearOutput(ActionFixture f) { f.Sent.Clear(); f.PeerSent.Clear(); f.Order.Clear(); }
    private static void AssertCoherent(ActionFixture f, uint? action = null, uint uid = Uid)
    {
        var current = f.Get(uid); var native = f.ConnectionFor(uid).NativeGameClient!;
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(native.OrdinarySpellBinding);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.True(binding.Items.Image.IsCurrentPlain());
        Assert.Same(f.Catalog, binding.ItemCatalog); Assert.Equal(CurrentAction(f, uid), (uint)native.Player!.Action);
        if (action is { } expected) Assert.Equal(expected, CurrentAction(f, uid));
        Assert.Equal(current.Character.MinHitPoints, (long)native.Player.HitPoints); Assert.Equal(current.Character.MinMana, (long)native.Player.Mana);
        Assert.Equal(ushort.Parse(current.Character.Attributes["Stamina"], CultureInfo.InvariantCulture), native.Player.Stamina);
        Assert.False(native.FullLoading); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        var saved = PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(File.ReadAllBytes(f.ItemsPath(uid))));
        Assert.Equal(saved, PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
        Assert.Equal(saved, PlayerItemSnapshotLoader.ToLegacyBytes(binding.Items.Image.FinalSnapshot));
    }
    private static void AssertFilesAndOwner(ActionFixture f, Dictionary<string, byte[]> files, uint action)
    { f.Data.AssertFiles(files); AssertCoherent(f, action); AssertCoherent(f, uid: Peer); }

    // Independent protobuf tags/varints. These whole-frame goldens never call
    // the Action/Skin/Update production writers to construct expected output.
    private static byte[] SkinGolden() { var body = new List<byte>(); Field(body, 1, 6); Field(body, 2, Uid); Field(body, 3, 3); return Frame(2415, body); }
    private static byte[] StanceGolden(uint value)
    {
        var body = new List<byte>(); Field(body, 1, Uid);
        if ((ushort)value != 0) Field(body, 3, (ushort)value);
        Field(body, 4, 17); Field(body, 5, 123456789); Field(body, 9, 123456);
        Field(body, 12, 81); Field(body, 13, 6); Field(body, 14, 430); Field(body, 15, 380); Field(body, 17, 1002);
        return Frame(2046, body);
    }
    private static byte[] StaminaGolden(ushort value)
    {
        var update = new List<byte>(); Field(update, 1, 8); Field(update, 2, value);
        var body = new List<byte>(); Field(body, 1, Uid); Varint(body, 18); Varint(body, checked((ulong)update.Count)); body.AddRange(update);
        return Frame(2176, body);
    }
    private static void Field(List<byte> bytes, int field, ulong value) { Varint(bytes, checked((ulong)field << 3)); Varint(bytes, value); }
    private static void Varint(List<byte> bytes, ulong value)
    { while (value >= 128) { bytes.Add((byte)((value & 127) | 128)); value >>= 7; } bytes.Add((byte)value); }
    private static byte[] Frame(ushort opcode, List<byte> body)
    {
        var bytes = new byte[body.Count + 4]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)bytes.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), opcode); body.CopyTo(bytes, 4); return bytes;
    }
    private static async Task Tick(ActionFixture f, ushort expected, bool held = false)
    {
        var files = f.Data.Files(); int count = f.Sent.Count, peers = f.PeerSent.Count;
        await f.TickAsync(); AssertCoherent(f); f.Data.AssertFiles(files); Assert.Equal(expected, Stamina(f));
        Assert.Equal(peers, f.PeerSent.Count);
        if (held) Assert.Equal(count, f.Sent.Count);
        else Assert.Equal(StaminaGolden(expected), Assert.Single(f.Sent.Skip(count)));
    }
    private static Dictionary<string, byte[]> ExpectedCoordinates(ActionFixture f, Dictionary<string, byte[]> before,
        params (uint Uid, long OldX, long OldY, long X, long Y)[] moves)
    {
        var result = before.ToDictionary(row => row.Key, row => row.Value, StringComparer.OrdinalIgnoreCase);
        foreach (var move in moves)
        {
            string path = Path.GetRelativePath(f.Data.Root, f.CharacterPath(move.Uid));
            string text = CharacterSnapshotFileEncoding.Decode(result[path], out var encoding);
            text = ReplaceLine(text, "X", move.OldX, move.X); text = ReplaceLine(text, "Y", move.OldY, move.Y);
            result[path] = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        }
        return result;
    }
    private static string ReplaceLine(string text, string key, long oldValue, long newValue)
    {
        string before = "\r\n" + key + "=" + oldValue.ToString(CultureInfo.InvariantCulture) + "\r\n";
        Assert.Equal(2, text.Split(before, StringSplitOptions.None).Length);
        return text.Replace(before, "\r\n" + key + "=" + newValue.ToString(CultureInfo.InvariantCulture) + "\r\n", StringComparison.Ordinal);
    }
}
