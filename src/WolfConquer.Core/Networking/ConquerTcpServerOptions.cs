using System.Net;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.Networking;

public sealed record ConquerTcpServerOptions(
    ConquerEndpoint Endpoint,
    IPAddress ListenAddress,
    int ListenPort,
    int MaxFrameSize = PacketHeader.DefaultMaxFrameSize,
    Func<ReadOnlyMemory<byte>>? ServerFirstPayloadFactory = null,
    ConquerTcpWireMode WireMode = ConquerTcpWireMode.Modern,
    LegacyGuardShieldTextProtocolOptions? GuardShieldTextProtocol = null,
    TextWriter? Log = null,
    TextWriter? ErrorLog = null);
