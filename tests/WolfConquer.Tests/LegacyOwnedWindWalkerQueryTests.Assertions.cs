using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedWindWalkerQueryTests
{
    private static bool IsVector(byte[] bytes, out ulong[] words)
    {
        words = [];
        if (!LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet) ||
            packet.Uid != Uid || packet.Updates.Count != 1 ||
            packet.Updates[0].Type != LegacyMsgUpdateDataType.StatusFlag || packet.Updates[0].Values is not { } values)
            return false;
        words = values.ToArray(); return true;
    }

    // Independent protobuf field/tag/varint oracle. Never calls the production
    // MsgUpdate writer or a native prepared publication to obtain expected bytes.
    private static byte[] VectorGolden(IReadOnlyList<ulong> words)
    {
        Assert.Equal(18, words.Count);
        var update = new List<byte> { 8, 25 };
        foreach (ulong word in words) { update.Add(24); Varint(update, word); }
        var body = new List<byte> { 8 }; Varint(body, Uid); body.Add(18); Varint(body, checked((ulong)update.Count)); body.AddRange(update);
        var frame = new byte[body.Count + 4]; BinaryPrimitives.WriteUInt16LittleEndian(frame, checked((ushort)frame.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2176); body.CopyTo(frame, 4); return frame;
    }
    private static void Varint(List<byte> bytes, ulong value)
    { while (value >= 128) { bytes.Add((byte)((value & 127) | 128)); value >>= 7; } bytes.Add((byte)value); }

    private static ulong[] Words(bool fan = false)
    { var result = new ulong[18]; if (fan) result[6] = 4; return result; }

    private static void AssertJoined(WindFixture f, bool present)
    {
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(f.Native.OrdinarySpellBinding!));
        Assert.True(f.Native.OrdinarySpellBinding!.Items.Image.IsCurrentPlain());
        Assert.Equal(present, (f.Vector.bits[6] & 4) != 0);
        Assert.Equal(present, f.Row is not null);
        Assert.Equal(present, LegacyPlayerDeathState.HasStatusFlag(f.Current.Character.Attributes, Fan));
        if (present)
        {
            var row = Assert.IsType<LegacyNativeStatusFlagsBigVector32.Flag>(f.Row);
            Assert.Equal(Fan, row.Key); Assert.Equal(2592000, row.Secounds);
            Assert.Equal(0, row.InvokerSecouds); Assert.False(row.RemoveOnDead);
            Assert.True(LegacyStatusFlagDeathRegistration.TryRead(f.Current.Character, Fan, out bool remove)); Assert.False(remove);
        }
        Assert.DoesNotContain(f.Current.PlayerSpells.Spells, row => row.Id == Fan);
        Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
    }

    private static void AssertOnlyFinalVector(IReadOnlyList<byte[]> output, bool added, ulong retainedWordSix = 0)
    {
        var indexed = output.Select((bytes, index) => (bytes, index)).Where(item => IsVector(item.bytes, out _)).ToArray();
        var vector = Assert.Single(indexed); Assert.Equal(output.Count - 1, vector.index);
        Assert.True(LegacyUserTotalRefineLevPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(output[^2]), out var refine));
        var words = Words(added); words[6] |= retainedWordSix;
        Assert.Equal(Uid, refine.Uid); Assert.Equal(VectorGolden(words), vector.bytes);
        Assert.DoesNotContain(output, bytes => OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode == 2215);
    }

    private static void AssertUnchangedSaved(WindFixture f, Dictionary<string, byte[]> before)
    {
        f.Owner.Data.AssertFiles(before);
        // The equipment WAL retains the physical preimage order and opaque
        // bytes. Parse supplies the source-defined logical equipment order;
        // the exact raw image was already checked above, without normalization.
        var logicalSaved = PlayerItemSnapshotLoader.ToLegacyBytes(
            PlayerItemSnapshotLoader.Parse(File.ReadAllBytes(f.Owner.ItemPath(Uid))));
        Assert.Equal(logicalSaved, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current.PlayerItems));
        Assert.Equal(logicalSaved,
            PlayerItemSnapshotLoader.ToLegacyBytes(f.Native.OrdinarySpellBinding!.Items.Image.FinalSnapshot));
    }
}
