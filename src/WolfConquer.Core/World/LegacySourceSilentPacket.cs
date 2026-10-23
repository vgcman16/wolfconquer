using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed class LegacySourceSilentPacket
{
    private const string SourceCatalogOnlyEvidence =
        "MrRayzo.Packets catalog only; no [Packet] attribute or Finalize(opcode) serializer found in decompiled GameServer";
    private const string SourceUnmappedEvidence =
        "no [Packet] attribute, Finalize(opcode) serializer, or MrRayzo.Packets/GamePackets catalog name found in decompiled GameServer";
    private const string SourceUnmapped2369Evidence =
        "opcode 2369 is absent from MrRayzo.Game.GamePackets and has no [Packet(2369)] attribute or Finalize(2369) serializer in decompiled GameServer; Program.Game_Receive only dispatches registered Pool.MsgInvoker handlers and otherwise sends no response; live log .setup_work\\runtime-parity-input-20260607-ready\\logs\\world.stdout.log captured 4 source-silent frames with payloadLength=74 and sample prefix 0800120208031202080412020807120208091202080C1202080F120208101202";
    private const string SourceUnmapped2433Evidence =
        "opcode 2433 is absent from MrRayzo.Game.GamePackets and has no [Packet(2433)] attribute or Finalize(2433) serializer in decompiled GameServer; Program.Game_Receive only dispatches registered Pool.MsgInvoker handlers and otherwise sends no response; live log .setup_work\\runtime-parity-input-20260607-ready\\logs\\world.stdout.log captured 9 source-silent frames with payloadLength=4 and payloads 08031000, 08051000, 08081000";
    private const string SourceUnmapped2311Evidence =
        "opcode 2311 has no [Packet(2311)] inbound handler or MrRayzo.Packets/GamePackets catalog name found in decompiled GameServer; MsgMessage.cs command ttp writes 21 literal payload bytes and calls Finalize(2311), then GetLeagueMainRank, without sending that packet; this serializer does not establish an inbound handler; Program.Game_Receive only dispatches registered Pool.MsgInvoker handlers and otherwise sends no response; live 2026-07-04 local world route observed an all-zero 516-byte client frame after login startup";

    public const ushort LegacyClientOpcode2038 = WorldPacketOpcodes.LegacySourceSilent2038;
    public const ushort LegacyClientOpcode2029 = WorldPacketOpcodes.LegacySourceSilent2029;
    public const ushort LegacyClientOpcode2145 = WorldPacketOpcodes.LegacySourceSilent2145;
    public const ushort LegacyClientOpcode2163 = WorldPacketOpcodes.LegacySourceSilent2163;
    public const ushort LegacyClientOpcode2196 = WorldPacketOpcodes.LegacySourceSilent2196;
    public const ushort LegacyClientOpcode2234 = WorldPacketOpcodes.LegacySourceSilent2234;
    // Retained opcode alias for callers; a native receiver exists, so this is not source-silent.
    public const ushort LegacyClientOpcode2307 = WorldPacketOpcodes.LegacyAchievement;
    public const ushort LegacyClientOpcode2311 = WorldPacketOpcodes.LegacySourceSilent2311;
    public const ushort LegacyClientOpcode2357 = WorldPacketOpcodes.LegacySourceSilent2357;
    public const ushort LegacyClientOpcode2369 = WorldPacketOpcodes.LegacySourceSilent2369;
    public const ushort LegacyClientOpcode2382 = WorldPacketOpcodes.LegacySourceSilent2382;
    public const ushort LegacyClientOpcode2383 = WorldPacketOpcodes.LegacySourceSilent2383;
    public const ushort LegacyClientOpcode2417 = WorldPacketOpcodes.LegacySourceSilent2417;
    public const ushort LegacyClientOpcode2424 = WorldPacketOpcodes.LegacySourceSilent2424;
    public const ushort LegacyClientOpcode2433 = WorldPacketOpcodes.LegacySourceSilent2433;
    public const ushort LegacyClientOpcode2456 = WorldPacketOpcodes.LegacySourceSilent2456;
    public const ushort LegacyClientOpcode2469 = WorldPacketOpcodes.LegacySourceSilent2469;

    public LegacySourceSilentPacket(ushort opcode, ReadOnlySpan<byte> payload)
    {
        if (!IsSourceSilentOpcode(opcode))
        {
            throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Opcode is not configured as a source-silent legacy client packet.");
        }

        Opcode = opcode;
        Payload = payload.ToArray();
    }

    public ushort Opcode { get; }

    public ReadOnlyMemory<byte> Payload { get; }

    public LegacySourceSilentOpcodeInfo Info => GetOpcodeInfo(Opcode);

    public static bool TryParse(PacketFrame frame, out LegacySourceSilentPacket packet)
    {
        packet = default!;

        if (!IsSourceSilentOpcode(frame.Header.Opcode))
        {
            return false;
        }

        packet = new LegacySourceSilentPacket(frame.Header.Opcode, frame.Payload.ToArray());
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, Payload.Length);
        Payload.Span.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    public static bool IsSourceSilentOpcode(ushort opcode)
    {
        return TryGetOpcodeInfo(opcode, out _);
    }

    public static LegacySourceSilentOpcodeInfo GetOpcodeInfo(ushort opcode)
    {
        if (TryGetOpcodeInfo(opcode, out var info))
        {
            return info;
        }

        throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Opcode is not configured as a source-silent legacy client packet.");
    }

    public static bool TryGetOpcodeInfo(ushort opcode, out LegacySourceSilentOpcodeInfo info)
    {
        info = opcode switch
        {
            LegacyClientOpcode2029 => new LegacySourceSilentOpcodeInfo(
                opcode,
                "CMsgRedEnvelops",
                SourceCatalogOnlyEvidence),
            LegacyClientOpcode2038 => new LegacySourceSilentOpcodeInfo(
                opcode,
                "CMsgTaskReward",
                SourceCatalogOnlyEvidence),
            LegacyClientOpcode2145 => new LegacySourceSilentOpcodeInfo(
                opcode,
                "CMsgAuctionItem",
                SourceCatalogOnlyEvidence),
            LegacyClientOpcode2163 => new LegacySourceSilentOpcodeInfo(
                opcode,
                "CMsgPing",
                SourceCatalogOnlyEvidence),
            LegacyClientOpcode2196 => new LegacySourceSilentOpcodeInfo(
                opcode,
                "CMsgTexasExInteractive",
                SourceCatalogOnlyEvidence),
            LegacyClientOpcode2234 => new LegacySourceSilentOpcodeInfo(
                opcode,
                "CMsgExpPool",
                SourceCatalogOnlyEvidence),
            LegacyClientOpcode2311 => new LegacySourceSilentOpcodeInfo(
                opcode,
                null,
                SourceUnmapped2311Evidence),
            LegacyClientOpcode2369 => new LegacySourceSilentOpcodeInfo(
                opcode,
                null,
                SourceUnmapped2369Evidence),
            LegacyClientOpcode2433 => new LegacySourceSilentOpcodeInfo(
                opcode,
                null,
                SourceUnmapped2433Evidence),
            LegacyClientOpcode2357 or
            LegacyClientOpcode2382 or
            LegacyClientOpcode2383 or
            LegacyClientOpcode2417 or
            LegacyClientOpcode2424 or
            LegacyClientOpcode2456 or
            LegacyClientOpcode2469 => new LegacySourceSilentOpcodeInfo(
                opcode,
                null,
                SourceUnmappedEvidence),
            _ => default,
        };

        return info.Opcode != 0;
    }
}

public readonly record struct LegacySourceSilentOpcodeInfo(
    ushort Opcode,
    string? SourceName,
    string SourceEvidence);
