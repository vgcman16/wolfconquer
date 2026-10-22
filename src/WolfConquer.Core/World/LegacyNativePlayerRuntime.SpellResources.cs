using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player877/1025/1149 physical storage, not session attributes.
    public ushort RandomSpell = 0;

    public ushort Stamina = 0;

    private ushort _mana;

    // Complete native Player2633..2653, commit before conditional owner send.
    public ushort Mana
    {
        get { return _mana; }
        set
        {
            _mana = value;
            if (Owner.FullLoading)
            {
                using (LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket())
                {
                    LegacyNativePacket stream = recycledPacket.GetStream();
                    SendUpdate(stream, value, LegacyNativeMsgUpdate.DataType.Mana);
                }
            }
        }
    }
}

public sealed partial class LegacyNativeGameClientRuntime
{
    // Native GameClient135; native spell admission owns updates, not CompleteLogin.
    public ushort OnSoulSpell = 0;
}
