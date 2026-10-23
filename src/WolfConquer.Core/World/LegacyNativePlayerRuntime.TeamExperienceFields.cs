namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Plain native fields: Player863,887,1117,1119,1209,1211,1273.
    public int CursedTimer = 0;
    public bool Reincarnation = false;
    // Native Player1111, before the saved reborn levels.
    public ushort Atributes;
    public byte FirstRebornLevel;
    public byte SecoundeRebornLevel;
    // Native Player1145, plain UInt32, not level-up side effects.
    public uint VirtutePoints;
    public uint DExpTime = 0u;
    public uint RateExp = 1u;
    internal LegacyNativeMsgSpell? FocusClientSpell;
}

public sealed partial class LegacyNativeGameClientRuntime
{
    // Native GameClient163 physical field. Full Oblivion removal caller is still held.
    public ulong ExpOblivion = 0uL;
}
