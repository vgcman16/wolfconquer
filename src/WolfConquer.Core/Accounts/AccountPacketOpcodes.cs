namespace WolfConquer.Core.Accounts;

public static class AccountPacketOpcodes
{
    public const ushort LegacyResourceProbe = 1052;
    public const ushort LegacyPasswordCryptographySeed = 1059;
    public const ushort LegacyForward = 1637;
    public const ushort LegacyAuthentication = 1942;

    // Internal fixture opcodes until passive captures provide the real client handshake.
    public const ushort InternalLoginRequest = 1000;
    public const ushort InternalLoginResponse = 1001;
}
