using ProtoBuf;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static void AppendMailboxPackageRewardFrames(MailboxPackagePrepared prepared, MailboxPackageStep step,
        LegacyMailboxPackageStackPlan.Materialization stock, List<byte[]> frames)
    {
        AppendMailboxPackageItemPhase(prepared, step.Original.Id, 0, stock, frames);
        if (prepared.SendClassExperience)
            frames.Add(new LegacyMsgUpdatePacket(checked((uint)prepared.Updated.Character.Uid),
                [new(LegacyMsgUpdateDataType.ClassExperience, step.ClassAfter)]).ToFrameBytes());
        AppendMailboxPackageItemPhase(prepared, step.Original.Id, 1, stock, frames);
        if (prepared.NinjaPublication is not null)
            frames.Add(PrepareMailboxPackageNinjaFrame(step.NinjaAfter));
        if (prepared.PiratePublication is not null && step.Original.Flag <= 7)
            frames.Add(prepared.PiratePublication.CopyFrameForPoints(step.PirateAccumulated));
        AppendMailboxPackageItemPhase(prepared, step.Original.Id, 2, stock, frames);
    }

    private static void AppendMailboxPackageItemPhase(MailboxPackagePrepared prepared, uint mailId, byte phase,
        LegacyMailboxPackageStackPlan.Materialization stock, List<byte[]> frames)
    {
        for (var index = 0; index < prepared.Grants.Length; index++)
            if (prepared.Grants[index].MailId == mailId && prepared.Grants[index].Phase == phase)
                foreach (var output in stock.Events.Where(output => output.GrantIndex == index))
                    frames.Add(LegacyGameItemPacket.FromSnapshotItem(output.Item, mode: output.Mode).ToFrameBytes());
    }

    private static byte[] PrepareMailboxPackageNinjaFrame(uint points)
    {
        // Same statically-object serializer and required-zero fields as native Type2.
        object value = new LegacyNativeMsgNinja.MsgNinjaUser { Type = 2, Points = points };
        using var bytes = new MemoryStream();
        Serializer.Serialize((Stream)bytes, value);
        var writer = new PacketWriter(LegacyNativeMsgNinja.MsgNinjaUser_ID, checked((int)bytes.Length));
        bytes.GetBuffer().AsSpan(0, checked((int)bytes.Length)).CopyTo(writer.Payload);
        return writer.ToArray();
    }
}
