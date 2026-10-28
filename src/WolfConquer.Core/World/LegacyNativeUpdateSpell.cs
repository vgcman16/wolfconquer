using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeUpdateSpell
{
    public static void CheckUpdate(LegacyNativePacket stream, LegacyNativeGameClientRuntime client,
        LegacyNativeInteractQuery Attack, uint Damage, Dictionary<ushort, LegacyNativeMagicType.Magic> DBSpells)
    {
        if (Damage == 0 || client is null || Attack is null) return;
        bool physical = Attack.AtkType is 2 or 28 or 24;
        if (DBSpells is null && !physical) return;
        LegacyNativeSpellStorage.RequireDirectEntry(client);
        var binding = LegacyNativeSpellStorage.GetJoinedBindingForClientEntry(client);
        if (binding is not null)
        {
            CheckUpdateJoined(stream, client, Attack, Damage, DBSpells, physical, binding);
            return;
        }
        var plan = TrainingPlan.TryPrepare(stream, client, Attack, Damage, DBSpells, physical);
        plan?.CommitUnbound(physical);
    }
}
