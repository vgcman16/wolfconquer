using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private sealed record StackItemOutput(byte Phase, PlayerItemSnapshotItem Item, ushort Mode);
    private sealed record StackMailOutput(LegacyMailboxEntry Mail, StackItemOutput[] Rewards,
        uint? ClassAfter, PlayerItemSnapshotItem? Attachment = null);

    private static StackItemOutput[] StackOutputs(IEnumerable<PlayerItemSnapshotItem> rows, ushort mode = LegacyGameItemPacket.UpdateItemMode) =>
        rows.Select((item, index) => new StackItemOutput((byte)(index < 4 ? 0 : 2), item, mode)).ToArray();

    private static void AssertStackFrames(Fixture fixture, PackageJoinBefore before, StackMailOutput[] steps,
        bool claimAll, DateTime started, DateTime ended, int firstFrame, byte[]? request = null)
    {
        var actual = fixture.Sent.Skip(firstFrame).ToArray();
        var cursor = 0;
        var money = checked((ulong)before.Owner.Character.Money);
        var cp = checked((ulong)before.Owner.Character.ConquerPoints);
        void Next(byte[] expected) { Assert.True(cursor < actual.Length); Assert.Equal(expected, actual[cursor++]); }
        foreach (var step in steps)
        {
            if (claimAll && step.Mail.Money != 0)
            {
                money = checked(money + step.Mail.Money);
                Next(LegacyMsgMessagePacket.SystemTalk($"STR_PICK_MONEY@@{step.Mail.Money}@@").ToFrameBytes());
                Next(new LegacyMsgUpdatePacket(Fixture.Uid, [new(LegacyMsgUpdateDataType.Money, money)]).ToFrameBytes());
            }
            if (claimAll && step.Mail.EMoney != 0)
            {
                cp = checked(cp + step.Mail.EMoney);
                Next(LegacyMsgMessagePacket.SystemTalk($"You received {step.Mail.EMoney} ConquerPoints.").ToFrameBytes());
                Next(new LegacyMsgUpdatePacket(Fixture.Uid, [new(LegacyMsgUpdateDataType.ConquerPoints, cp)]).ToFrameBytes());
            }
            foreach (var item in step.Rewards.Where(item => item.Phase == 0))
                Next(LegacyGameItemPacket.FromSnapshotItem(item.Item, mode: item.Mode).ToFrameBytes());
            if (step.ClassAfter.HasValue)
                Next(new LegacyMsgUpdatePacket(Fixture.Uid, [new(LegacyMsgUpdateDataType.ClassExperience, step.ClassAfter.Value)]).ToFrameBytes());
            foreach (var item in step.Rewards.Where(item => item.Phase == 2))
                Next(LegacyGameItemPacket.FromSnapshotItem(item.Item, mode: item.Mode).ToFrameBytes());
            if (step.Attachment is not null) Next(LegacyGameItemPacket.FromSnapshotItem(step.Attachment).ToFrameBytes());
            if (claimAll)
            {
                Assert.True(cursor < actual.Length);
                Assert.True(LegacyMailListPacket.TryParse(ReadFrame(actual[cursor++]), ConquerProtocolProfile.VisibleDx8D13, out var refresh));
                Assert.Equal(LegacyMailListType.ClaimAllUpdate, refresh.Type);
                var row = Assert.Single(refresh.Mails);
                var mail = step.Mail.WithClearedFlag().WithClaimedMoney().WithClearedItemId();
                var earliest = LegacyMailListPacket.FromMailboxEntry(mail, started);
                var latest = LegacyMailListPacket.FromMailboxEntry(mail, ended);
                Assert.Equal(earliest with { ExpirationDate = row.ExpirationDate }, row);
                Assert.InRange(row.ExpirationDate, latest.ExpirationDate, earliest.ExpirationDate);
            }
        }
        Next(request ?? PackageJoinRequest(claimAll));
        Assert.Equal(cursor, actual.Length);
    }
}
