using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private static byte[] MailboxMoneyRequest() =>
        new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimMoney, AtomicMailId, []).ToFrameBytes();

    private static Dictionary<string, byte[]> ExpectedMailboxMoneyFiles(Fixture fixture, Snapshot before)
    {
        var expected = new Dictionary<string, byte[]>(before.Files, StringComparer.OrdinalIgnoreCase);
        var mailPath = Path.GetRelativePath(fixture.Root, fixture.AtomicMailPath);
        var mail = before.Files[mailPath].ToArray();
        Assert.Equal(4 + 2 * 740, mail.Length);
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(mail));
        Assert.Equal(AtomicMailId, BinaryPrimitives.ReadUInt32LittleEndian(mail.AsSpan(4 + 704)));
        Assert.Equal(fixture.AtomicMailSeed.ItemId, BinaryPrimitives.ReadUInt32LittleEndian(mail.AsSpan(4 + 720)));
        Assert.Equal(fixture.AtomicMailSeed.Flag, BinaryPrimitives.ReadUInt32LittleEndian(mail.AsSpan(4 + 728)));
        // Existing managed codec only: clear Money (8) and EMoney (4).
        // ItemId, package Flag, record type, text, timer and the other row remain exact.
        mail.AsSpan(4 + 708, 12).Clear();
        expected[mailPath] = mail;
        var values = new Dictionary<string, string>();
        if (fixture.AtomicMailSeed.Money != 0)
            values["Money"] = checked(before.Owner.Character.Money + checked((long)fixture.AtomicMailSeed.Money))
                .ToString(CultureInfo.InvariantCulture);
        if (fixture.AtomicMailSeed.EMoney != 0)
            values["ConquerPoints"] = checked(before.Owner.Character.ConquerPoints + fixture.AtomicMailSeed.EMoney)
                .ToString(CultureInfo.InvariantCulture);
        var characterPath = Path.GetRelativePath(fixture.Root, fixture.CharacterPath);
        expected[characterPath] = ProjectSashFields(before.Files[characterPath], values);
        return expected; // Includes the exact unchanged complete item image and unrelated files.
    }

    private static byte[][] ExpectedMailboxMoneyFrames(Fixture fixture, Snapshot before)
    {
        var frames = new List<byte[]>();
        var reward = fixture.AtomicMailSeed;
        if (reward.Money != 0)
        {
            frames.Add(LegacyMsgMessagePacket.SystemTalk($"STR_PICK_MONEY@@{reward.Money}@@").ToFrameBytes());
            frames.Add(new LegacyMsgUpdatePacket(Fixture.Uid, [new(LegacyMsgUpdateDataType.Money,
                checked((ulong)before.Owner.Character.Money + reward.Money))]).ToFrameBytes());
        }
        if (reward.EMoney != 0)
        {
            frames.Add(LegacyMsgMessagePacket.SystemTalk($"You received {reward.EMoney} ConquerPoints.").ToFrameBytes());
            frames.Add(new LegacyMsgUpdatePacket(Fixture.Uid, [new(LegacyMsgUpdateDataType.ConquerPoints,
                checked((ulong)before.Owner.Character.ConquerPoints + reward.EMoney))]).ToFrameBytes());
        }
        frames.Add(MailboxMoneyRequest());
        return frames.ToArray();
    }

    private static void AssertMailboxMoneyCommitted(Fixture fixture, Snapshot before)
    {
        fixture.AssertFiles(ExpectedMailboxMoneyFiles(fixture, before));
        var loadedItems = new PlayerItemSnapshotLoader(fixture.Items).LoadByCharacterUid(Fixture.Uid);
        Assert.Equal(JsonSerializer.Serialize(before.Owner.PlayerItems), JsonSerializer.Serialize(loadedItems));
        Assert.DoesNotContain(loadedItems.InventoryItems, row => row.Uid == AtomicMailItemUid);
        Assert.Single(loadedItems.WarehouseItems, row => row.Uid == AtomicMailItemUid && row.WarehouseId == 1000);
        var loadedMail = Assert.Single(new LegacyMailboxStore(fixture.Root).Load(Fixture.Uid), row => row.Id == AtomicMailId);
        Assert.Equal(JsonSerializer.Serialize(fixture.AtomicMailSeed.WithClaimedMoney()), JsonSerializer.Serialize(loadedMail));
        var character = CharacterSnapshotParser.ParseText(File.ReadAllText(fixture.CharacterPath), fixture.CharacterPath);
        Assert.Equal(checked(before.Owner.Character.Money + checked((long)fixture.AtomicMailSeed.Money)), character.Money);
        Assert.Equal(checked(before.Owner.Character.ConquerPoints + fixture.AtomicMailSeed.EMoney), character.ConquerPoints);
        Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
    }

    private static void AssertMailboxMoneyFrames(Fixture fixture, Snapshot before, int first)
    {
        var expected = ExpectedMailboxMoneyFrames(fixture, before);
        var actual = fixture.Sent.Skip(first).ToArray();
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++) Assert.Equal(expected[index], actual[index]);
    }

    private static async Task AssertMailboxMoneyReplayAsync(Fixture fixture, Snapshot before)
    {
        var first = fixture.Sent.Count;
        await fixture.FrameAsync(MailboxMoneyRequest());
        AssertMailboxMoneyCommitted(fixture, before);
        Assert.Equal(MailboxMoneyRequest(), Assert.Single(fixture.Sent.Skip(first)));
    }
}
