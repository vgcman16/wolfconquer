using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed class LegacyNativePreparedSelfStatusPublication : IDisposable
{
    private readonly LegacyNativeSelfStatusBindings bindings;
    private readonly LegacyNativeStatusFlagsBigVector32.SelfStatusChange token;
    private readonly bool stigma, present;
    private readonly uint[]? words;
    private readonly byte[]? statusFrame;
    private readonly byte[] animationFrame;
    private LegacyNativeRecycledPacket? status, animation;
    private IntPtr statusAddress, animationAddress;
    private bool copied, borrowedCaller;

    private LegacyNativePreparedSelfStatusPublication(LegacyNativeSelfStatusBindings bindings,
        LegacyNativeStatusFlagsBigVector32.SelfStatusChange token, bool stigma, bool present,
        uint[]? words, byte[]? statusFrame, byte[] animationFrame)
    {
        this.bindings = bindings; this.token = token; this.stigma = stigma; this.present = present;
        this.words = words; this.statusFrame = statusFrame; this.animationFrame = animationFrame;
    }

    internal static LegacyNativePreparedSelfStatusPublication Create(LegacyNativeSelfStatusBindings bindings,
        LegacyNativeStatusFlagsBigVector32.SelfStatusChange token, bool stigma, bool present, uint[]? words)
    {
        // The real animation is owned here; no arbitrary supplied payload can be
        // paired with a genuine status token. Staged words are copied privately.
        uint[]? privateWords = words?.ToArray();
        if (present != (privateWords is null)) throw new InvalidDataException("Self-status publication policy does not match its prepared words.");
        using var animation = new LegacyNativeMsgSpellAnimation(bindings.Uid, 0, bindings.X, bindings.Y,
            stigma ? (ushort)1095 : (ushort)1020, 4, 0);
        if (!present) animation.Targets.Enqueue(new(bindings.Uid, 0, LegacyNativeMsgAttackPacket.AttackEffect.None));
        byte[] animationPayload = animation.PrepareAnimationPayload();
        string? seal = LegacyNativePacket.SealString;
        byte[]? statusBytes = privateWords is null ? null :
            Frame(2176, LegacyNativeMsgUpdate.PrepareStatusFlagPayload(bindings.Uid, privateWords), seal);
        byte[] animationBytes = Frame(2239, animationPayload, seal);
        var prepared = new LegacyNativePreparedSelfStatusPublication(bindings, token, stigma, present, privateWords, statusBytes, animationBytes);
        try
        {
            if (!bindings.HasOriginalCaller(animationBytes.Length))
                throw new InvalidDataException("Self-status caller cannot hold the complete prepared animation.");
            if (statusBytes is not null)
            {
                prepared.status = new();
                prepared.MarkBorrowedCaller(prepared.status.GetStream());
            }
            prepared.animation = new();
            prepared.MarkBorrowedCaller(prepared.animation.GetStream());
            if (!prepared.ValidateLoans()) throw new InvalidDataException("Self-status loans must be live, bounded and distinct from the caller.");
            prepared.CaptureAndCopy();
            return prepared;
        }
        catch { prepared.Dispose(); throw; }
    }

    private static byte[] Frame(ushort opcode, byte[] payload, string? seal)
    {
        if (seal is not null && seal.Length != 8) throw new InvalidDataException("Invalid native self-status packet seal.");
        int sealSize = seal is null ? 0 : 8;
        if (4L + payload.Length + sealSize > 1032) throw new InvalidDataException("Self-status packet exceeds the native send bound.");
        var bytes = new byte[4 + payload.Length + sealSize];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)(payload.Length + 4));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), opcode);
        payload.CopyTo(bytes, 4);
        if (seal is not null) Buffer.BlockCopy(Encoding.UTF8.GetBytes(seal), 0, bytes, payload.Length + 4, 8);
        return bytes;
    }

    private void MarkBorrowedCaller(LegacyNativePacket packet)
    {
        if (ReferenceEquals(packet, bindings.Caller)) borrowedCaller = true;
    }

    private unsafe bool ValidateLoans()
    {
        var a = animation?.GetStream(); var s = status?.GetStream();
        if (!bindings.HasOriginalCaller(animationFrame.Length) || !Live(a, animationFrame.Length) ||
            Overlap(a!, bindings.Caller)) return false;
        return statusFrame is null || (Live(s, statusFrame.Length) && !Overlap(s!, a!) && !Overlap(s!, bindings.Caller));
    }

    private static unsafe bool Live(LegacyNativePacket? packet, int length) =>
        packet is not null && packet.Memory != null && packet.Capacity >= length;

    private static unsafe bool Overlap(LegacyNativePacket left, LegacyNativePacket right)
    {
        if (ReferenceEquals(left, right)) return true;
        ulong a = (ulong)(nuint)left.Memory, b = (ulong)(nuint)right.Memory;
        if (a == 0 || b == 0) return false;
        // Capacities are immutable positive allocation sizes. Difference-based
        // comparison avoids address+length overflow at the native-width boundary.
        return a <= b ? b - a < (ulong)left.Capacity : a - b < (ulong)right.Capacity;
    }

    private unsafe void CaptureAndCopy()
    {
        var a = animation!.GetStream(); animationAddress = (IntPtr)a.Memory;
        if (statusFrame is not null)
        {
            var s = status!.GetStream(); statusAddress = (IntPtr)s.Memory;
            Copy(s, statusFrame);
        }
        Copy(a, animationFrame); copied = true;
    }

    internal bool HasLiveStorage() => copied && ValidateLoans() && PreparedLoansMatch();

    internal bool MatchesAcceptance(LegacyNativeSelfStatusBindings expected,
        LegacyNativeStatusFlagsBigVector32.SelfStatusChange expectedToken, int flag, bool expectedPresent, uint[]? expectedWords) =>
        ReferenceEquals(bindings, expected) && ReferenceEquals(token, expectedToken) && present == expectedPresent &&
        flag == (int)(stigma ? LegacyNativeMsgUpdate.Flags.Stigma : LegacyNativeMsgUpdate.Flags.MagicShield) &&
        (words is null ? expectedWords is null : expectedWords is not null && words.AsSpan().SequenceEqual(expectedWords));

    private bool PreparedLoansMatch() => Match(animation?.GetStream(), animationFrame, animationAddress) &&
        (statusFrame is null || Match(status?.GetStream(), statusFrame, statusAddress));

    private static unsafe bool Match(LegacyNativePacket? packet, byte[] bytes, IntPtr address)
    {
        if (!Live(packet, bytes.Length) || (IntPtr)packet!.Memory != address ||
            packet.Size != bytes.Length || packet.Position != 4) return false;
        for (int index = 0; index < bytes.Length; index++) if (packet.Memory[index] != bytes[index]) return false;
        return true;
    }

    private static unsafe void Copy(LegacyNativePacket packet, byte[] bytes)
    {
        Marshal.Copy(bytes, 0, (IntPtr)packet.Memory, bytes.Length);
        packet.Size = bytes.Length; packet.Seek(4);
    }

    internal void CopyCallerAndPublish(LegacyNativeSelfStatusBindings expected)
    {
        if (!ReferenceEquals(bindings, expected) || !bindings.IsCurrent() || !HasLiveStorage()) return;
        Copy(bindings.Caller, animationFrame);
        if (status is not null) Publish(status.GetStream());
        Publish(animation!.GetStream());
    }

    private void Publish(LegacyNativePacket packet)
    {
        if (!bindings.IsCurrent() || !PreparedLoansMatch()) return;
        try { bindings.Client.Send(packet); }
        catch (Exception error) { LegacyNativePlayerRuntime.ReportFlagAdmission("self-status owner delivery after commit", error); }
        if (!bindings.IsCurrent() || !PreparedLoansMatch()) return;
        try { bindings.View.SendView(packet, false); }
        catch (Exception error) { LegacyNativePlayerRuntime.ReportFlagAdmission("self-status view delivery after commit", error); }
    }

    public void Dispose()
    {
        DisposeOne(ref status); DisposeOne(ref animation);
        if (borrowedCaller) { borrowedCaller = false; bindings.RestoreRejectedBorrowCursor(); }
    }

    private static void DisposeOne(ref LegacyNativeRecycledPacket? rental)
    {
        var owned = rental; rental = null;
        if (owned is null) return;
        try { owned.Dispose(); }
        catch (Exception error) { LegacyNativePlayerRuntime.ReportFlagAdmission("self-status packet cleanup", error); }
    }
}
