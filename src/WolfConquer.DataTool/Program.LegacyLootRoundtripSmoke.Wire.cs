using System.Net.Sockets;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

internal static partial class Program
{
    private sealed class LegacyLootRoundtripWire : IDisposable
    {
        private readonly TcpClient client;
        private readonly NetworkStream stream;
        private readonly LegacyGameServerCipher inbound;
        private readonly LegacyGameServerCipher outbound;
        private readonly List<byte> pending = new();
        private readonly byte[] readBuffer = new byte[4096];
        private readonly uint characterUid;
        private uint nonce = unchecked((uint)Environment.TickCount);
        private int framesRead;

        private LegacyLootRoundtripWire(TcpClient client, byte[] key, uint characterUid)
        {
            this.client = client;
            stream = client.GetStream();
            inbound = new LegacyGameServerCipher(key);
            outbound = new LegacyGameServerCipher(key);
            this.characterUid = characterUid;
        }

        internal static async Task<LegacyLootRoundtripWire> ConnectAsync(
            LegacyLootRoundtripSmokeOptions options, CancellationToken token)
        {
            var client = new TcpClient(options.Address.AddressFamily);
            try
            {
                await client.ConnectAsync(options.Address, options.Port, token).ConfigureAwait(false);
                var stream = client.GetStream();
                var handshake = await ReadEncryptedLegacyWorldHandshakeAsync(stream, token).ConfigureAwait(false);
                var values = ReadLegacyWorldHandshakeDhValues(handshake);
                var dh = new LegacyGameServerDiffieHellman(values.PrimeHex, values.GeneratorHex);
                var publicKey = dh.GenerateRequest();
                dh.HandleResponse(values.ServerPublicHex);
                await stream.WriteAsync(EncryptLegacyWorldWithLoginKey(
                    CreateLegacyWorldClientHandshakeReply(publicKey)), token).ConfigureAwait(false);
                await stream.FlushAsync(token).ConfigureAwait(false);
                var key = new byte[LegacyGameServerCipher.KeyMaterialSize];
                dh.DeriveKeyMaterial(key);
                var wire = new LegacyLootRoundtripWire(client, key, options.CharacterUid);
                await wire.SendAsync(new LegacyMsgLoginGamePacket(options.AccountKey).ToFrameBytes(), token).ConfigureAwait(false);
                return wire;
            }
            catch (Exception ex) when (ex is FormatException or ArithmeticException or ArgumentException or InvalidOperationException)
            {
                client.Dispose();
                throw new InvalidDataException("Malformed or unusable legacy WorldServer handshake.", ex);
            }
            catch { client.Dispose(); throw; }
        }

        internal async Task SendAsync(byte[] frame, CancellationToken token)
        {
            var encrypted = LegacyGameServerFrameCodec.EncodeEncrypted(frame, outbound, LegacyGameServerFrameSeal.Client);
            await stream.WriteAsync(encrypted, token).ConfigureAwait(false);
            await stream.FlushAsync(token).ConfigureAwait(false);
        }

        internal async Task<PacketFrame> ReadAsync(CancellationToken token)
        {
            while (true)
            {
                if (TryTakeLegacyWorldSmokeFrame(pending, out var frame))
                {
                    if (++framesRead > 16_384) throw new InvalidDataException("Wire frame limit exceeded.");
                    ValidateLootRoundtripFrame(frame);
                    return frame;
                }
                var read = await stream.ReadAsync(readBuffer, token).ConfigureAwait(false);
                if (read == 0) throw new EndOfStreamException("WorldServer closed the encrypted connection.");
                var decrypted = readBuffer.AsSpan(0, read).ToArray();
                inbound.DecryptInPlace(decrypted);
                pending.AddRange(decrypted);
                if (pending.Count > PacketHeader.DefaultMaxFrameSize * 2)
                    throw new InvalidDataException("Unframed wire buffer limit exceeded.");
            }
        }

        internal async Task BarrierAsync(Action<PacketFrame> observe, CancellationToken token)
        {
            var value = unchecked(++nonce);
            var ping = new MsgItemUsagePacket(characterUid, value, MsgItemUsageAction.Ping, value, 0, 0, 0);
            await SendAsync(ping.ToFrameBytes(), token).ConfigureAwait(false);
            while (true)
            {
                var frame = await ReadAsync(token).ConfigureAwait(false);
                if (MsgItemUsagePacket.TryParse(frame, out var echo) && echo == ping) return;
                observe(frame);
            }
        }

        internal async Task FinishAsync(CancellationToken token)
        {
            // Half-close and require real server EOF before opening another
            // connection. This does not manufacture server session removal.
            client.Client.Shutdown(SocketShutdown.Send);
            try { while (true) _ = await ReadAsync(token).ConfigureAwait(false); }
            catch (EndOfStreamException) when (pending.Count == 0) { }
        }

        public void Dispose() => client.Dispose();
    }

    static void ValidateLootRoundtripFrame(PacketFrame frame)
    {
        var opcode = frame.Header.Opcode;
        bool valid = opcode switch
        {
            LegacyGameItemPacket.Opcode => LegacyGameItemPacket.TryParse(frame, out _),
            LegacyFloorItemPacket.Opcode => LegacyFloorItemPacket.TryParse(frame, out _),
            MsgItemUsagePacket.Opcode => MsgItemUsagePacket.TryParse(frame, out _),
            MsgActionPacket.Opcode => MsgActionPacket.TryParse(frame, out _),
            LegacyMsgMessagePacket.Opcode => LegacyMsgMessagePacket.TryParse(frame, out _),
            LegacyLoaderHeroInfoPacket.Opcode => LegacyLoaderHeroInfoPacket.TryParse(frame, out _),
            LegacyServerVersionPacket.Opcode => LegacyServerVersionPacket.TryParse(frame, out _),
            LegacyNpcReplyPacket.Opcode => LegacyNpcReplyPacket.TryParse(frame, out _),
            VisibleDx8D13TrainingVitalityInfoPacket.Opcode => VisibleDx8D13TrainingVitalityInfoPacket.TryParse(frame, out _),
            LegacyUserAbilityScorePacket.Opcode => LegacyUserAbilityScorePacket.TryParse(frame, out _),
            _ => true,
        };
        if (!valid) throw new InvalidDataException($"Malformed known wire packet opcode={opcode}.");
        if (opcode == LegacyChiInfoPacket.Opcode)
            throw new InvalidDataException("RootD11 chi opcode received; this smoke requires explicit VisibleDx8D13.");
        if (LegacyNpcReplyPacket.TryParse(frame, out var npc) &&
            (npc.InteractType != LegacyNpcReplyInteractType.StaticGui || npc.Unknown != 0 ||
             npc.InputMaxLength != 0 || !Enum.IsDefined(typeof(LegacyStaticGuiMode), npc.OptionId)))
            throw new InvalidDataException("Unexpected NPC interaction: loot operation may require interaction or was rejected.");
        if (!LegacyMsgMessagePacket.TryParse(frame, out var message)) return;
        var text = message.Content[3];
        if (text == "NEW_ROLE" ||
            message.ChatType == LegacyMsgMessagePacket.DialogChatType && text != "ANSWER_OK" ||
            text.Contains("PARITY-GAP", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("PARITY-BUG", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("rejected", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("not modeled", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Unexpected server diagnostic/dialog opcode={opcode} chatType={message.ChatType}.");
    }
}
