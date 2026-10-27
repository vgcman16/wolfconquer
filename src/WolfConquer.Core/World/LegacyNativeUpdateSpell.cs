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
        var plan = TrainingPlan.TryPrepare(stream, client, Attack, Damage, DBSpells, physical);
        if (plan is null || !plan.IsCurrent()) return;
        if (plan.HasSpell) plan.UpdateProficiencies();
        if (plan.HasProgress)
        {
            // Proficiency sends can perform a legitimate COW spell mutation.
            // Never write the earlier captured row after those callouts.
            if (!plan.CommitProgress()) return;
            plan.PublishProgress();
        }
        if (physical && plan.IsCurrent()) plan.UpdateProficiencies();
    }
}
