namespace WolfConquer.Core.World;

// Complete native MsgLoginClient physical declaration. LoginGame/LoginHandler
// remain whole typed-held; this record does not install a receive/accept route.
internal partial struct LegacyNativeMsgLoginClient
{
    public ushort Length;

    public ushort PacketID;

    public uint AccountHash;

    public uint Key;

    public uint HDSerial;

    public string? MachineName;

    public string? MacAddress;

    public string? TokenLogin;

    public string? Language;
}
