using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using ProtoBuf;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeExchangeShopRuntime
{
    public uint GetTime()
    {
        DateTime now = DateTime.Now;
        return RemainingUntilNextDailyReset(now);
    }

    public void Open(LegacyNativePacket stream, uint NpcID, uint ShopID)
    {
        if (!TryOpen(stream, NpcID, ShopID, out var reason)) ReportRefusal(nameof(Open), reason);
    }

    public unsafe bool TryOpen(LegacyNativePacket? stream, uint NpcID, uint ShopID, out string reason)
    {
        lock (bookSyncRoot)
        {
            byte[] frame;
            LegacyNativePacket target;
            try
            {
                if (!TryPrepareOpenPublication(stream, NpcID, ShopID, out frame, out target, out reason)) return false;
            }
            catch (Exception error) { reason = PreparationFailure(error); return false; }

            PublishPreparedOpenFrame(target, frame);
            reason = string.Empty;
            return true; // Publication attempted, never a delivery acknowledgment.
        }
    }

    private unsafe bool TryPrepareOpenPublication(LegacyNativePacket? stream, uint npcId, uint shopId,
        out byte[] frame, out LegacyNativePacket target, out string reason)
    {
        frame = null!;
        target = null!;
        if (!TryCaptureBook(out var book, out reason)) return false;
        if (!TryResolveOpenPlayer(out var player, out reason)) return false;
        if (stream is null || stream.Memory == null)
        { reason = "exchangeShopPacketUnavailable"; return false; }
        uint uid = player!.UID;
        var message = PrepareOpenMessage(book!, npcId, shopId, uid);
        using var serialized = new MemoryStream();
        Serializer.Serialize(serialized, message);
        byte[] payload = serialized.ToArray();
        string? seal = LegacyNativePacket.SealString;
        int sealSize = seal is null ? 0 : 8;
        long required = 4L + payload.Length + sealSize;
        if (required > 1032 || required > stream.Capacity)
        { reason = "exchangeShopPacketTooLarge"; return false; }
        frame = CreateOpenFrame(payload, seal);
        if (!TryRevalidateOpenPublication(book!, player!, uid, stream, out reason)) return false;
        target = stream;
        return true;
    }

    private bool TryResolveOpenPlayer(out LegacyNativePlayerRuntime? player, out string reason)
    {
        player = user?.Player;
        if (player is null || !ReferenceEquals(player.Owner, user) || !ReferenceEquals(user!.MyExchangeShop, this))
        { reason = "exchangeShopOwnerInvalid"; return false; }
        reason = string.Empty;
        return true;
    }

    private LegacyNativeCMsgExchangeShopGoods PrepareOpenMessage(BookState book, uint npcId, uint shopId, uint uid)
    {
        var rows = book.Rows.Where(row => row.ShopId == shopId).ToArray();
        return new LegacyNativeCMsgExchangeShopGoods
        {
            NpcID = npcId, ShopID = shopId, UID = uid, TimeInSeconds = GetTime(),
            Items = rows.Select(row => new LegacyNativeCMsgExchangeShopGoods.Item
            {
                Index = row.GoodsId, Count = row.Times, uk = 0u
            }).ToArray()
        };
    }

    private unsafe bool TryRevalidateOpenPublication(BookState book, LegacyNativePlayerRuntime player,
        uint uid, LegacyNativePacket stream, out string reason)
    {
        if (!BookIsCurrent(book) || !ReferenceEquals(user.MyExchangeShop, this) || !ReferenceEquals(user.Player, player) ||
            !ReferenceEquals(player.Owner, user) || player.UID != uid)
        { reason = "exchangeShopStateChanged"; return false; }
        if (stream.Memory == null)
        { reason = "exchangeShopPacketUnavailable"; return false; }
        reason = string.Empty;
        return true;
    }

    private unsafe void PublishPreparedOpenFrame(LegacyNativePacket target, byte[] frame)
    {
        // First caller write is the publication-attempt boundary. Preparation
        // failures leave caller bytes untouched; transport/logging failure here
        // cannot honestly become an unchanged rejection.
        try
        {
            Marshal.Copy(frame, 0, (IntPtr)target.Memory, frame.Length);
            target.Size = frame.Length;
            target.Seek(4); // Same post-Finalize cursor as the genuine builder.
            user.Send(target);
        }
        catch (Exception error) { ReportPublicationFailure(error); }
    }

    private static byte[] CreateOpenFrame(byte[] payload, string? seal)
    {
        int sealSize = seal is null ? 0 : 8;
        var frame = new byte[4 + payload.Length + sealSize];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, (ushort)(4 + payload.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2317);
        Buffer.BlockCopy(payload, 0, frame, 4, payload.Length);
        if (seal is not null)
        {
            // Native Write(string,8) copies the first eight UTF8 bytes of its
            // eight-character seal. Capture once so one frame has one seal view.
            var bytes = Encoding.UTF8.GetBytes(seal);
            Buffer.BlockCopy(bytes, 0, frame, 4 + payload.Length, 8);
        }
        return frame;
    }

    private static void ReportPublicationFailure(Exception error)
    {
        try { Console.WriteLine($"ExchangeShop Open publication attempted; delivery unproven: {error.GetType().Name}"); }
        catch { /* Output/logging failure cannot turn attempted publication into refusal. */ }
    }
}
