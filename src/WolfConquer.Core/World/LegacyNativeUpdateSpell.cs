using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

// Whole native UpdateSpell component. Dispatcher/host acceptance is separately held.
internal partial class LegacyNativeUpdateSpell
{
    public static void CheckUpdate(LegacyNativePacket stream, LegacyNativeGameClientRuntime client, LegacyNativeInteractQuery Attack, uint Damage, Dictionary<ushort, LegacyNativeMagicType.Magic> DBSpells)
    {
        if (Damage == 0 || client == null || Attack == null)
            return;

        LegacyNativeMsgSpell? spell = null;
        LegacyNativeMagicType.Magic? row = null;
        var player = client.Player;
        if (DBSpells != null)
        {
            var spells = client.MySpells;
            if (spells == null)
                return;
            if (spells.ClientSpells.TryGetValue(Attack.SpellID, out var found))
            {
                if (found == null)
                    return;
                spell = found;
                if (spell.Level < DBSpells.Count - 1 &&
                    (!DBSpells.TryGetValue(spell.Level, out row) || row == null || (!row.isRuneSkill && player == null)))
                    return;
            }
        }

        bool physical = Attack.AtkType == 2 || Attack.AtkType == 28 || Attack.AtkType == 24;
        if (spell == null && !physical)
            return;
        var equipment = client.Equipment;
        if (equipment == null || stream == null)
            return;
        uint right = equipment.RightWeapon / 1000;
        uint left = equipment.LeftWeapon / 1000;
        var profs = client.MyProfs;
        if ((right != 0 || left != 0) && profs == null)
            return;

        // Prepare representable, nonnegative spell XP before either real
        // proficiency pass. Ineligible spell branches do not consume this rule.
        int? preparedSpellExperience = null;
        if (spell != null && row != null && !row.isRuneSkill && player != null && player.Level >= row.NeedLevel)
        {
            if (!TryPrepareSpellExperience(spell.Experience, Damage,
                    LegacyNativeServerConfig.ExpRateSpell, out var experience))
                return;
            preparedSpellExperience = experience;
        }

        if (spell != null)
        {
            UpdateProficiencies(profs, right, left, Damage, stream);
            ushort level = spell.Level;
            if (preparedSpellExperience is { } experience && row != null)
            {
                spell.Experience = experience;
                if (spell.Experience > row.Experience)
                {
                    spell.Level++;
                    spell.Experience = 0;
                }
                if (spell.PreviousLevel != 0 && spell.PreviousLevel >= spell.Level / 2)
                    spell.Level = spell.PreviousLevel;
                try
                {
                    if (spell.Level > level)
                        client.SendSysMesage("You increased the spell level!");
                }
                catch (Exception ex)
                {
                    LegacyNativeConsole.WriteLine(ex.ToString());
                }
                client.Send(stream.SpellCreate(spell));
            }
        }
        if (physical)
            UpdateProficiencies(profs, right, left, Damage, stream);
    }

    private static void UpdateProficiencies(LegacyNativeProficiencyStorage? profs, uint right, uint left, uint damage, LegacyNativePacket stream)
    {
        if (profs == null)
            return; // Preflight permits this only when both weapon IDs are zero.
        if (right != 0)
            profs.CheckUpdate(right, damage * LegacyNativeServerConfig.ExpRateProf, stream);
        if (left != 0)
            profs.CheckUpdate(left, damage * LegacyNativeServerConfig.ExpRateProf, stream);
    }
}
