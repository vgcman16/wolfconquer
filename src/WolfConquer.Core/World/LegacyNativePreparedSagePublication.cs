using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed class LegacyNativePreparedSagePublication : IDisposable
{
    private LegacyNativeRecycledPacket? status;
    private LegacyNativeRecycledPacket? archive;
    private int statusLength;
    private int archiveLength;
    private byte[]? preparedStatus;
    private byte[] preparedArchive = [];

    private readonly LegacyNativeSageModeBindings expectedBindings;
    private readonly LegacyNativeStatusFlagsBigVector32.PreparedChange? expectedChange;
    private readonly uint timer;
    private readonly uint[]? expectedWords;

    private LegacyNativePreparedSagePublication(LegacyNativeSageModeBindings bindings, uint timer,
        LegacyNativeStatusFlagsBigVector32.PreparedChange? change, uint[]? words)
    {
        expectedBindings = bindings; this.timer = timer;
        expectedChange = change; expectedWords = words;
    }

    internal static LegacyNativePreparedSagePublication Create(LegacyNativeSageModeBindings bindings, uint timer,
        LegacyNativeStatusFlagsBigVector32.PreparedChange? change)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        if (!Monitor.IsEntered(bindings.Vector.SyncRoot) || !bindings.IsCurrent())
            throw new InvalidDataException("Sage preparation requires its current physical owner gate.");
        uint[]? statusWords = null;
        if (change is not null && !bindings.Vector.TryGetPreparedWords(change, out statusWords))
            throw new InvalidDataException("Sage preparation requires its real current vector token.");
        if (!bindings.IsCurrent()) throw new InvalidDataException("Sage preparation owner changed during validation.");
        var publication = new LegacyNativePreparedSagePublication(bindings, timer, change, statusWords);
        uint uid = bindings.Uid;
        try
        {
            string? seal = LegacyNativePacket.SealString;
            byte[]? statusFrame = statusWords is null ? null :
                Frame(LegacyNativeMsgUpdate.PrepareStatusFlagPayload(uid, statusWords), seal);
            byte[] archiveFrame = Frame(LegacyNativeMsgUpdate.PrepareSageArchivePayload(uid, timer), seal);
            publication.preparedStatus = statusFrame;
            publication.preparedArchive = archiveFrame;
            if (statusFrame is not null)
            {
                publication.status = new();
                publication.statusLength = statusFrame.Length;
            }
            publication.archive = new();
            publication.archiveLength = archiveFrame.Length;
            if (!publication.HasLiveStorage()) throw new InvalidDataException("Sage updates require distinct bounded live packets.");
            // Validate BOTH loans before copying either frame. An aliased second
            // rental cannot overwrite a prepared status frame with archive bytes.
            if (statusFrame is not null) CopyFrame(publication.status!.GetStream(), statusFrame);
            CopyFrame(publication.archive.GetStream(), archiveFrame);
            return publication;
        }
        catch { publication.Dispose(); throw; }
    }

    private static byte[] Frame(byte[] payload, string? seal)
    {
        if (seal is not null && seal.Length != 8) throw new InvalidDataException("Sage packet seal is not a valid native seal.");
        int sealSize = seal is null ? 0 : 8;
        if (4L + payload.Length + sealSize > 1032) throw new InvalidDataException("Sage update exceeds native send bound.");
        var frame = new byte[4 + payload.Length + sealSize];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)(payload.Length + 4));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2176);
        Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);
        if (seal is not null) Buffer.BlockCopy(Encoding.UTF8.GetBytes(seal), 0, frame, 4 + payload.Length, 8);
        return frame;
    }

    internal unsafe bool HasLiveStorage()
    {
        var statusPacket = status?.GetStream();
        var archivePacket = archive?.GetStream();
        return archivePacket is not null && archivePacket.Memory != null && archivePacket.Capacity >= archiveLength &&
            (status is null || (statusPacket is not null && statusPacket.Memory != null &&
                statusPacket.Capacity >= statusLength && !ReferenceEquals(statusPacket, archivePacket)));
    }

    internal bool HasPreparedStorage() => HasLiveStorage() &&
        HasPreparedFrame(archive!.GetStream(), preparedArchive) &&
        (preparedStatus is null || HasPreparedFrame(status!.GetStream(), preparedStatus));

    private static unsafe bool HasPreparedFrame(LegacyNativePacket packet, byte[] expected)
        => packet.Size == expected.Length && packet.Position == 4 &&
            new ReadOnlySpan<byte>(packet.Memory, expected.Length).SequenceEqual(expected);

    private static unsafe void CopyFrame(LegacyNativePacket packet, byte[] frame)
    {
        Marshal.Copy(frame, 0, (IntPtr)packet.Memory, frame.Length);
        packet.Size = frame.Length;
        packet.Seek(4);
    }

    internal bool MatchesPreparedPlan(LegacyNativeSageModeBindings bindings,
        LegacyNativeStatusFlagsBigVector32.PreparedChange change, uint[] words, int seconds) =>
        ReferenceEquals(expectedBindings, bindings) && ReferenceEquals(expectedChange, change) &&
        expectedWords is not null && expectedWords.AsSpan().SequenceEqual(words) &&
        preparedStatus is not null && seconds == unchecked((int)timer);

    internal bool MatchesDuplicate(LegacyNativeSageModeBindings bindings) =>
        ReferenceEquals(expectedBindings, bindings) && expectedChange is null &&
        expectedWords is null && preparedStatus is null && HasPreparedStorage();

    internal void Publish()
    {
        if (status is not null) PublishFrame(expectedBindings, status);
        PublishFrame(expectedBindings, archive!);
    }

    private static void PublishFrame(LegacyNativeSageModeBindings bindings, LegacyNativeRecycledPacket rental)
    {
        if (!bindings.IsCurrent()) return;
        try { bindings.Client.Send(rental.GetStream()); }
        catch (Exception error) { LegacyNativePlayerRuntime.ReportFlagAdmission("Sage owner publication after commit", error); }
        if (!bindings.IsCurrent()) return;
        try { bindings.View.SendView(rental.GetStream(), false); }
        catch (Exception error) { LegacyNativePlayerRuntime.ReportFlagAdmission("Sage view publication after commit", error); }
    }

    public void Dispose()
    {
        DisposeOne(ref status); DisposeOne(ref archive);
    }

    private static void DisposeOne(ref LegacyNativeRecycledPacket? rental)
    {
        var owned = rental; rental = null;
        if (owned is null) return;
        try { owned.Dispose(); }
        catch (Exception error) { LegacyNativePlayerRuntime.ReportFlagAdmission("Sage packet cleanup", error); }
    }
}
