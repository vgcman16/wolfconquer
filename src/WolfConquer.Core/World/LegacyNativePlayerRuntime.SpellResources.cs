using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player877/1025/1149 physical storage, not session attributes.
    public ushort RandomSpell = 0;

    public ushort Stamina = 0;

    private uint _mana;

    // Ordinary admitted players require their actual private Store-first joint
    // participant. This public component setter refuses owned attachments.
    // Whole unbound components retain the native conditional-send contract.
    public uint Mana
    {
        get { return Volatile.Read(ref _mana); }
        set { SetCurrentMana(value); }
    }
}

public sealed partial class LegacyNativeGameClientRuntime
{
    // Native GameClient135; native spell admission owns updates, not CompleteLogin.
    public ushort OnSoulSpell = 0;
}
