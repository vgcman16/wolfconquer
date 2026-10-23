using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed class LegacyNativePreparedRefinementPublication : IDisposable
{
    private sealed class Loan(byte[] frame)
    {
        internal readonly byte[] Frame = frame;
        internal LegacyNativeRecycledPacket? Rental;
        internal IntPtr Address;
        internal LegacyNativePacket? Packet => Rental?.GetStream();
    }

    private readonly LegacyNativeRefinementBindings bindings;
    private readonly LegacyNativeStatusFlagsBigVector32.RefinementChange token;
    private readonly LegacyNativePerfectionEffects.RefinementKind kind;
    private readonly bool eligible;
    private readonly uint[]? words;
    private readonly Loan[] loans;
    private bool copied;

    private LegacyNativePreparedRefinementPublication(LegacyNativeRefinementBindings bindings,
        LegacyNativeStatusFlagsBigVector32.RefinementChange token,
        LegacyNativePerfectionEffects.RefinementKind kind, bool eligible, uint[]? words, Loan[] loans)
    {
        this.bindings = bindings; this.token = token; this.kind = kind; this.eligible = eligible;
        this.words = words; this.loans = loans;
    }

    internal static LegacyNativePreparedRefinementPublication Create(LegacyNativeRefinementBindings bindings,
        LegacyNativeStatusFlagsBigVector32.RefinementChange token)
    {
        if (!bindings.Vector.TryGetRefinementPublication(token, bindings, out var kind, out bool eligible, out var words))
            throw new InvalidDataException("Refinement has no current genuine status preparation.");
        string? seal = LegacyNativePacket.SealString;
        Loan[] loans = PrepareFrames(bindings.Uid, kind, eligible, words, seal);
        var prepared = new LegacyNativePreparedRefinementPublication(bindings, token, kind, eligible, words, loans);
        try
        {
            foreach (var loan in loans) loan.Rental = new();
            // Do not copy even the first frame until ALL real loans are bounded,
            // non-overlapping allocations. Rejected alias rentals cannot dirty bytes.
            if (!prepared.ValidateLoans()) throw new InvalidDataException("Refinement loans are invalid or aliased.");
            foreach (var loan in loans) CaptureAndCopy(loan);
            prepared.copied = true;
            return prepared;
        }
        catch { prepared.Dispose(); throw; }
    }

    private static Loan[] PrepareFrames(uint uid, LegacyNativePerfectionEffects.RefinementKind kind,
        bool eligible, uint[]? words, string? seal)
    {
        bool stamina = kind == LegacyNativePerfectionEffects.RefinementKind.Stamina;
        if (stamina && !eligible) return [];
        if (eligible && (stamina ? words is not null : words is null))
            throw new InvalidDataException("Refinement staged status policy does not match its frame.");
        var effect = kind switch
        {
            LegacyNativePerfectionEffects.RefinementKind.Mirror => LegacyNativeMsgRefineEffect.RefineEffects.MirrorOfSin,
            LegacyNativePerfectionEffects.RefinementKind.Killing => LegacyNativeMsgRefineEffect.RefineEffects.KillingFlash,
            LegacyNativePerfectionEffects.RefinementKind.Stamina => LegacyNativeMsgRefineEffect.RefineEffects.LightOfStamina,
            _ => throw new InvalidDataException("Unknown refinement frame."),
        };
        var refinement = new Loan(Frame(2319, LegacyNativeMsgRefineEffect.PrepareRefinementPayload(uid, effect), seal));
        if (!eligible) return [refinement];
        var scalar = new Loan(Frame(2176, LegacyNativeMsgUpdate.PrepareRefinementScalarPayload(uid, stamina), seal));
        return stamina ? [refinement, scalar] :
            [refinement, new(Frame(2176, LegacyNativeMsgUpdate.PrepareStatusFlagPayload(uid, words!), seal)), scalar];
    }

    private static byte[] Frame(ushort opcode, byte[] payload, string? seal)
    {
        if (seal is not null && seal.Length != 8) throw new InvalidDataException("Invalid native refinement seal.");
        int sealSize = seal is null ? 0 : 8;
        if (4L + payload.Length + sealSize > 1032) throw new InvalidDataException("Refinement frame exceeds native send bound.");
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

    internal bool MatchesAcceptance(LegacyNativeRefinementBindings expected,
        LegacyNativeStatusFlagsBigVector32.RefinementChange expectedToken,
        LegacyNativePerfectionEffects.RefinementKind expectedKind, bool expectedEligible, uint[]? expectedWords) =>
        ReferenceEquals(bindings, expected) && ReferenceEquals(token, expectedToken) && kind == expectedKind &&
        eligible == expectedEligible && (words is null ? expectedWords is null :
            expectedWords is not null && words.AsSpan().SequenceEqual(expectedWords));

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
        // gameplay result and cannot authorize a repeat grant or stamina refund.
        int count = grant ? loans.Length : Math.Min(loans.Length, 1);
        if (kind == LegacyNativePerfectionEffects.RefinementKind.Stamina && !grant) count = 0;
        for (int index = 0; index < count; index++)
        {
            if (!bindings.IsCurrent() || !HasLiveStorage()) return;
            var packet = loans[index].Packet!;
            try { bindings.Client.Send(packet); }
            catch (Exception error) { LegacyNativePlayerRuntime.ReportFlagAdmission("refinement owner delivery after acceptance", error); }
            bool scalar = grant && index == count - 1 && index > 0;
            if (scalar || !bindings.IsCurrent() || !HasLiveStorage()) continue;
            try { bindings.View.SendView(packet, false); }
            catch (Exception error) { LegacyNativePlayerRuntime.ReportFlagAdmission("refinement view delivery after acceptance", error); }
        }
    }

    public void Dispose()
    {
        foreach (var loan in loans)
        {
            var rental = loan.Rental; loan.Rental = null;
            if (rental is null) continue;
            try { rental.Dispose(); }
            catch (Exception error) { LegacyNativePlayerRuntime.ReportFlagAdmission("refinement packet cleanup", error); }
        }
    }
}
