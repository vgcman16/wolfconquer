using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeSubClassRuntime
{
    private sealed class StudyPreparation : IDisposable
    {
        internal readonly byte[] Effect, Animation;
        internal LegacyNativePacket? Chat { get; private set; }
        private LegacyNativeRecycledPacket? chatRental;
        private readonly string? seal;
        private readonly byte[] sealBytes;
        private byte[]? chatBytes;
        private nint chatMemory;

        internal StudyPreparation(StudyBinding binding, StudyPacketTarget target,
            ushort after, ushort? award)
        {
            seal = CurrentSeal();
            sealBytes = seal is null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(seal).AsSpan(0, 8).ToArray();
            Effect = Prepare(packet => packet.StringPacketCreate(new LegacyNativeMsgStringPacket
            {
                UID = binding.Uid, ID = LegacyNativeMsgStringPacket.StringID.Effect,
                Strings = new[] { "zf2-e300" }
            }));
            Animation = Prepare(packet => packet.SubClassCreate(LegacyNativeMsgSubClass.Action.Animation, after, 0));
            target.RequireStorage(Math.Max(Effect.Length, Animation.Length));
            if (award is not null)
            {
                var message = new LegacyNativeMsgMessage("You received " + award.Value + " StudyPoints.",
                    LegacyNativeMsgMessage.MsgColor.red, LegacyNativeMsgMessage.ChatMode.Talk);
                chatBytes = Prepare(packet => message.GetArray(packet));
            }
            binding.RequireCurrent();
            target.RequireUnchanged();
            RequireCurrent();
            // All other managed/frame/target checks precede the real native rental.
            if (chatBytes is not null) RentChat(target);
        }

        private static string? CurrentSeal() => LegacyNativePacket.SealSize == 0 ? null : LegacyNativePacket.SealString;

        private unsafe byte[] Prepare(Func<LegacyNativePacket, LegacyNativePacket> encode)
        {
            using var scratch = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
            if (!ReferenceEquals(encode(scratch), scratch) || scratch.Size < 4 ||
                scratch.Size > 1032 || scratch.Size > scratch.Capacity || scratch.Position != 4)
                throw new InvalidOperationException("Study encoder did not prepare a bounded native frame.");
            var frame = new byte[scratch.Size];
            Marshal.Copy((nint)scratch.Memory, frame, 0, frame.Length);
            int declared = BinaryPrimitives.ReadUInt16LittleEndian(frame);
            if (declared < 4 || declared + sealBytes.Length != frame.Length ||
                !frame.AsSpan(declared).SequenceEqual(sealBytes))
                throw new InvalidOperationException("Study frame does not match its captured native seal.");
            return frame;
        }

        private unsafe void RentChat(StudyPacketTarget target)
        {
            try
            {
                chatRental = new LegacyNativeRecycledPacket();
                Chat = chatRental.GetStream();
                if (ReferenceEquals(Chat, target.Packet))
                {
                    Dispose();
                    target.RestoreRentalCursor();
                    throw new InvalidOperationException("Native chat rental aliases the study caller packet.");
                }
                if (Chat is null || Chat.Memory == null || chatBytes!.Length > Chat.Capacity)
                    throw new InvalidOperationException("Native chat rental has no complete frame capacity.");
                chatMemory = (nint)Chat.Memory;
                Marshal.Copy(chatBytes, 0, chatMemory, chatBytes.Length);
                Chat.Size = chatBytes.Length;
                Chat.Seek(4);
                RequireCurrent();
            }
            catch (Exception error)
            {
                Dispose();
                if (error is InvalidOperationException) throw;
                throw new InvalidOperationException("Native study chat rental preparation failed.", error);
            }
        }

        internal unsafe void RequireCurrent()
        {
            if (seal != CurrentSeal()) throw new InvalidOperationException("Native study seal changed during preparation.");
            if (Chat is not null && (Chat.Memory == null || (nint)Chat.Memory != chatMemory ||
                Chat.Size != chatBytes!.Length || Chat.stream != Chat.Memory + 4 ||
                !new ReadOnlySpan<byte>(Chat.Memory, chatBytes.Length).SequenceEqual(chatBytes)))
                throw new InvalidOperationException("Prepared native chat storage changed.");
        }

        public void Dispose()
        {
            var rental = chatRental;
            chatRental = null;
            Chat = null;
            // Reuse retains the native Seek(0) behavior; it does not reenqueue.
            rental?.Dispose();
        }
    }
}
