using WolfConquer.Core.Packets;

namespace WolfConquer.Core.Runtime;

public static class ConquerRuntimeDefaults
{
    public const string ServerName = "TestSrv";
    public const string ServerAddress = "127.0.0.1";

    public static string WebsiteUrl =>
        Environment.GetEnvironmentVariable("WOLFCONQUER_WEBSITE_URL") ?? "https://example.invalid/";

    public static string VoteUrl =>
        Environment.GetEnvironmentVariable("WOLFCONQUER_VOTE_URL") ?? "https://example.invalid/vote";
    public const int GatewayPort = 2021;
    public const int MySqlPort = 3306;
    public const int WorldPort = 5818;
    public const int AccountPort = 9960;
    public const int ReceiveSize = PacketHeader.DefaultMaxFrameSize;
    public const int SendSize = PacketHeader.DefaultMaxFrameSize;

    public static ConquerEndpoint AccountServer { get; } = new("AccountServer", ServerAddress, AccountPort);

    public static ConquerEndpoint Gateway { get; } = new("Gateway", ServerAddress, GatewayPort);

    public static ConquerEndpoint WorldServer { get; } = new("WorldServer", ServerAddress, WorldPort);
}
