using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed class LegacyNativePreparedDivineGuardPublication : IDisposable
{
    private sealed class Loan(byte[] frame)
    {
        internal readonly byte[] Frame = frame;
        internal LegacyNativeRecycledPacket? Rental;
        internal IntPtr Address;
        internal LegacyNativePacket? Packet => Rental?.GetStream();
    }

    private readonly LegacyNativeDivineGuardBindings bindings;
    private readonly LegacyNativeStatusFlagsBigVector32.DivineGuardChange token;
    private readonly bool present;
    private readonly uint[]? words;
    private readonly Loan[] loans;
    private bool copied;

    private LegacyNativePreparedDivineGuardPublication(LegacyNativeDivineGuardBindings bindings,
        LegacyNativeStatusFlagsBigVector32.DivineGuardChange token,
        bool present, uint[]? words, Loan[] loans)
    {
        this.bindings = bindings; this.token = token; this.present = present;
        this.words = words; this.loans = loans;
    }

    internal static LegacyNativePreparedDivineGuardPublication Create(LegacyNativeDivineGuardBindings bindings,
        LegacyNativeStatusFlagsBigVector32.DivineGuardChange token)
    {
        if (!bindings.Vector.TryGetDivineGuardPublication(token, bindings, out bool present, out var words))
            throw new InvalidDataException("DivineGuard has no current genuine status preparation.");
        if (!bindings.IsCurrent()) throw new InvalidDataException("DivineGuard preparation no longer owns its player.");
        string? seal = LegacyNativePacket.SealString;
        Loan[] loans = PrepareFrames(bindings.Uid, present, words, seal);
        var prepared = new LegacyNativePreparedDivineGuardPublication(bindings, token, present, words, loans);
        try
        {
            foreach (var loan in loans) loan.Rental = new();
            // Do not copy even the first frame until ALL real loans are bounded,
            // non-overlapping allocations. Rejected alias rentals cannot dirty bytes.
            if (!prepared.ValidateLoans()) throw new InvalidDataException("DivineGuard loans are invalid or aliased.");
            foreach (var loan in loans) CaptureAndCopy(loan);
            prepared.copied = true;
            return prepared;
        }
        catch { prepared.Dispose(); throw; }
    }

    private static Loan[] PrepareFrames(uint uid, bool present, uint[]? words, string? seal)
    {
        if (present != (words is null))
            throw new InvalidDataException("DivineGuard status plan does not match its frames.");
        var refinement = new Loan(Frame(2319, LegacyNativeMsgRefineEffect.PrepareRefinementPayload(uid,
            LegacyNativeMsgRefineEffect.RefineEffects.DivineGuard), seal));
        if (present) return [refinement];
        return [refinement,
            new(Frame(2176, LegacyNativeMsgUpdate.PrepareStatusFlagPayload(uid, words!), seal)),
            new(Frame(2176, LegacyNativeMsgUpdate.PrepareDivineGuardPayload(uid), seal))];
    }

    private static byte[] Frame(ushort opcode, byte[] payload, string? seal)
    {
        if (seal is not null && seal.Length != 8) throw new InvalidDataException("Invalid native DivineGuard seal.");
        int sealSize = seal is null ? 0 : 8;
        if (4L + payload.Length + sealSize > 1032) throw new InvalidDataException("DivineGuard frame exceeds native send bound.");
        var bytes = new byte[4 + payload.Length + sealSize];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, (ushort)(payload.Length + 4));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), opcode);
        payload.CopyTo(bytes, 4);
        if (seal is not null) Buffer.BlockCopy(Encoding.UTF8.GetBytes(seal), 0, bytes, payload.Length + 4, 8);
        return bytes;
    }

    private unsafe bool ValidateLoans()
    {
        for (int index = 0; index < loans.Length; index++)
        {
            var loan = loans[index];
            if (loan.Packet is not { } packet || packet.Memory == null || packet.Capacity < loan.Frame.Length) return false;
            for (int prior = 0; prior < index; prior++)
                if (Overlap(packet, loans[prior].Packet!)) return false;
        }
        return true;
    }

    private static unsafe bool Overlap(LegacyNativePacket left, LegacyNativePacket right)
    {
        if (ReferenceEquals(left, right)) return true;
        ulong a = (ulong)(nuint)left.Memory, b = (ulong)(nuint)right.Memory;
        return a <= b ? b - a < (ulong)left.Capacity : a - b < (ulong)right.Capacity;
    }

    private static unsafe void CaptureAndCopy(Loan loan)
    {
        var packet = loan.Packet!;
        loan.Address = (IntPtr)packet.Memory;
        Marshal.Copy(loan.Frame, 0, loan.Address, loan.Frame.Length);
        packet.Size = loan.Frame.Length;
        packet.Seek(4);
    }

    internal bool MatchesAcceptance(LegacyNativeDivineGuardBindings expected,
        LegacyNativeStatusFlagsBigVector32.DivineGuardChange expectedToken,
        bool expectedPresent, uint[]? expectedWords) =>
        ReferenceEquals(bindings, expected) && ReferenceEquals(token, expectedToken) && present == expectedPresent &&
        (words is null ? expectedWords is null : expectedWords is not null && words.AsSpan().SequenceEqual(expectedWords));

    internal unsafe bool HasLiveStorage()
    {
        if (!copied || !ValidateLoans()) return false;
        foreach (var loan in loans)
        {
            var packet = loan.Packet!;
            if ((IntPtr)packet.Memory != loan.Address || packet.Size != loan.Frame.Length || packet.Position != 4) return false;
            for (int index = 0; index < loan.Frame.Length; index++)
                if (packet.Memory[index] != loan.Frame[index]) return false;
        }
        return true;
    }

    internal void Publish(bool grant)
    {
        // Acceptance already happened. Transport rejection/failure is not a false
        // gameplay result and cannot authorize a repeat grant or cooldown refund.
        int count = grant ? loans.Length : Math.Min(loans.Length, 1);
        for (int index = 0; index < count; index++)
        {
            if (!bindings.IsAcceptedCurrent() || !HasLiveStorage()) return;
            var packet = loans[index].Packet!;
            try { bindings.Client.Send(packet); }
            catch (Exception error) { LegacyNativePlayerRuntime.ReportFlagAdmission("DivineGuard owner delivery after acceptance", error); }
            bool archive = grant && index == count - 1 && index > 0;
            if (archive || !bindings.IsAcceptedCurrent() || !HasLiveStorage()) continue;
            try { bindings.View.SendView(packet, false); }
            catch (Exception error) { LegacyNativePlayerRuntime.ReportFlagAdmission("DivineGuard view delivery after acceptance", error); }
        }
    }

    public void Dispose()
    {
        foreach (var loan in loans)
        {
            var rental = loan.Rental; loan.Rental = null;
            if (rental is null) continue;
            try { rental.Dispose(); }
            catch (Exception error) { LegacyNativePlayerRuntime.ReportFlagAdmission("DivineGuard packet cleanup", error); }
        }
    }
}
