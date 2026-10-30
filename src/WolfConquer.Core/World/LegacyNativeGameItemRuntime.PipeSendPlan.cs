using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeGameItemRuntime
{
    private sealed unsafe class PreparedPipeItemFrame : IDisposable
    {
        internal readonly LegacyNativePacket Packet;
        internal readonly byte[] Bytes;
        private readonly string? seal;
        private readonly byte[] sealBytes;
        private readonly ushort opcode;

        private PreparedPipeItemFrame(LegacyNativePacket packet, byte[] bytes, string? seal,
            byte[] sealBytes, ushort opcode)
        {
            Packet = packet; Bytes = bytes; this.seal = seal;
            this.sealBytes = sealBytes; this.opcode = opcode;
        }

        internal static PreparedPipeItemFrame Create(int capacity, ushort opcode, string? seal,
            byte[] sealBytes, Action<LegacyNativePacket> encode)
        {
            var frame = new LegacyNativePacket(capacity);
            try
            {
                encode(frame);
                RequireFraming(frame, opcode, seal, sealBytes);
                byte[] bytes = new ReadOnlySpan<byte>(frame.Memory, frame.Size).ToArray();
                return new(frame, bytes, seal, sealBytes, opcode);
            }
            catch { frame.Dispose(); throw; }
        }

        internal void RequireCurrent()
        {
            RequireFraming(Packet, opcode, seal, sealBytes);
            if (!new ReadOnlySpan<byte>(Packet.Memory, Packet.Size).SequenceEqual(Bytes))
                throw new InvalidOperationException("Prepared pipe item bytes changed before commitment.");
        }

        private static void RequireFraming(LegacyNativePacket frame, ushort opcode, string? seal, byte[] sealBytes)
        {
            if (!string.Equals(seal, LegacyNativePacket.SealString, StringComparison.Ordinal) ||
                frame.Memory == null || frame.stream != frame.Memory + 4 || frame.Size < 4 ||
                frame.Size > frame.Capacity)
                throw new InvalidOperationException("Prepared pipe item frame has invalid physical state.");
            var bytes = new ReadOnlySpan<byte>(frame.Memory, frame.Size);
            int declared = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
            if (declared < 4 || declared + sealBytes.Length != bytes.Length ||
                BinaryPrimitives.ReadUInt16LittleEndian(bytes[2..]) != opcode ||
                !bytes[declared..].SequenceEqual(sealBytes))
                throw new InvalidOperationException("Prepared pipe item header or original seal is invalid.");
        }

        public void Dispose() => Packet.Dispose();
    }

    private sealed class PipeItemPlan : IDisposable
    {
        private readonly LegacyNativeItemPacketSnapshot image;
        private readonly LegacyNativeItemExtra.Purification purification;
        private readonly LegacyNativeItemExtra.Refinery refinery;
        internal readonly PreparedPipeItemFrame Item;
        internal readonly PreparedPipeItemFrame? Extra;
        internal byte[] FinalBytes => (Extra ?? Item).Bytes;

        internal PipeItemPlan(LegacyNativeItemPacketSnapshot image, PreparedPipeItemFrame item,
            PreparedPipeItemFrame? extra, LegacyNativeItemExtra.Purification purification,
            LegacyNativeItemExtra.Refinery refinery)
        {
            this.image = image; Item = item; Extra = extra;
            this.purification = purification; this.refinery = refinery;
        }

        internal void RequireCurrent()
        {
            if (!image.IsCurrent()) throw new InvalidOperationException("Actual pipe item changed during preparation.");
            Item.RequireCurrent(); Extra?.RequireCurrent();
        }

        internal void RequireCapacity(int capacity)
        {
            if (Item.Bytes.Length > capacity || (Extra is not null && Extra.Bytes.Length > capacity))
                throw new InvalidOperationException("A prepared pipe output exceeds the actual caller capacity.");
        }

        internal void CommitExtras()
        {
            if (Extra is null) return; // Both zero UIDs retain the unused-expiry gate.
            image.Source.Purification = purification;
            image.Source.Refinary = refinery;
        }

        public void Dispose() { Item.Dispose(); Extra?.Dispose(); }
    }

    private PipeItemPlan PreparePipeItem(int capacity)
    {
        var image = new LegacyNativeItemPacketSnapshot(this);
        uint remaining = RemainingTime;
        uint minutes = TimeLeftInMinutes;
        string? seal = LegacyNativePacket.SealString;
        byte[] sealBytes = seal is null ? Array.Empty<byte>() : CapturePipeItemSeal(seal);
        PreparedPipeItemFrame? item = null, extra = null;
        try
        {
            item = PreparedPipeItemFrame.Create(capacity, 2116, seal, sealBytes,
                prepared => WriteCapturedItem(prepared, image, remaining, minutes));
            var purification = image.Purification;
            var refinery = image.Refinary;
            if (purification.ItemUID != 0 || refinery.ItemUID != 0)
            {
                var message = new LegacyNativeItemExtra();
                if (purification.InLife)
                {
                    purification.Typ = purification.SecoundsLeft == 0
                        ? LegacyNativeItemExtra.Typing.Stabilization : LegacyNativeItemExtra.Typing.PurificationAdding;
                    message.Purifications.Add(purification);
                }
                if (refinery.InLife)
                {
                    refinery.Typ = refinery.EffectDuration == 0
                        ? LegacyNativeItemExtra.Typing.PermanentRefinery : LegacyNativeItemExtra.Typing.RefinaryAdding;
                    message.Refinerys.Add(refinery);
                }
                // Keep the real empty extra when retained nonzero IDs expire,
                // and the real interserver raw EffectID rather than display301.
                extra = PreparedPipeItemFrame.Create(capacity, 2134, seal, sealBytes,
                    prepared => message.CreateArray(prepared, tointer: true));
            }
            var plan = new PipeItemPlan(image, item, extra, purification, refinery);
            plan.RequireCurrent();
            return plan;
        }
        catch { item?.Dispose(); extra?.Dispose(); throw; }
    }

    private static byte[] CapturePipeItemSeal(string seal)
    {
        if (seal.Length != 8) throw new InvalidOperationException("Pipe item seal has invalid width.");
        // Preserve native fixed-width UTF8 byte truncation.
        return Encoding.UTF8.GetBytes(seal).AsSpan(0, 8).ToArray();
    }
}
