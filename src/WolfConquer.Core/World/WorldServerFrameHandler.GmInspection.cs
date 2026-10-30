using System.Globalization;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    // Called only by the admitted GM ingress under its real exclusive world lease.
    // These are bounded runtime diagnostics, never saved-file or atomicity proofs.
    private string[] CreateGmInspectionLines(LegacyGmInspectionSection section,
        LegacyRuntimeInspectionSnapshot snapshot, WorldSession session)
    {
        var character = snapshot.ManagedCharacter;
        var lines = new List<string> { $"WolfConquer GM: {section} (runtime diagnostic)" };
        switch (section)
        {
            case LegacyGmInspectionSection.Overview:
                lines.Add($"UID {snapshot.CharacterUid}; level {character.Level}; class {character.Class}; body {character.Body}.");
                lines.Add($"Map {character.Map}; position {character.X},{character.Y}; profile {snapshot.ProtocolProfile}.");
                lines.Add($"Gold {character.Gold}; CPs {character.ConquerPoints}; inventory {session.PlayerItems.InventoryItems.Count}; equipment {session.PlayerItems.EquipmentItems.Count}.");
                lines.Add($"Spells {session.PlayerSpells.Spells.Count}; proficiencies {session.PlayerProficiencies.Proficiencies.Count}; quests {session.Character.PlayerQuests.Quests.Count}.");
                break;
            case LegacyGmInspectionSection.Position:
                lines.Add($"Map {character.Map}; X {character.X}; Y {character.Y}.");
                lines.Add($"Previous map {session.Character.PreviousMapId}; X {session.Character.PreviousMapX}; Y {session.Character.PreviousMapY}.");
                break;
            case LegacyGmInspectionSection.Vitals:
                lines.Add($"Native HP {snapshot.NativeVitals.Hitpoints}; mana {snapshot.NativeVitals.Mana}.");
                lines.Add($"Managed HP {Optional(character.Hitpoints)}; mana {Optional(character.MinMana)}; max mana {Optional(character.MaxMana)}.");
                lines.Add("Values are observed diagnostics; derived native maxima and saved files are not verified.");
                break;
            case LegacyGmInspectionSection.Inventory:
            case LegacyGmInspectionSection.Equipment:
                var container = section == LegacyGmInspectionSection.Inventory ? "inventory" : "equipment";
                var items = snapshot.ManagedItems.Where(row => row.Container == container).ToArray();
                lines.Add($"Managed {container}: {items.Length} roots. Showing at most 9.");
                lines.AddRange(items.Take(9).Select(row => $"UID {row.Uid}; item {row.ItemId}; slot {row.Position}; +{row.Plus}; stack {row.StackSize}; dura {row.Durability}/{row.MaximumDurability}."));
                if (items.Length == 0) lines.Add("No roots in the observed container.");
                break;
            case LegacyGmInspectionSection.Spells:
                lines.Add($"Known spells: {session.PlayerSpells.Spells.Count}. Showing at most 9.");
                lines.AddRange(session.PlayerSpells.Spells.Take(9).Select(row => $"Spell {row.Id}; level {row.Level}; XP {row.Experience}; soul {row.SoulLevel}; active soul {row.UseSpellSoul}."));
                break;
            case LegacyGmInspectionSection.Proficiencies:
                lines.Add($"Weapon proficiencies: {session.PlayerProficiencies.Proficiencies.Count}. Showing at most 9.");
                lines.AddRange(session.PlayerProficiencies.Proficiencies.Take(9).Select(row => $"Weapon {row.Id}; level {row.Level}; XP {row.Experience}/{row.NeededExperience}."));
                break;
            case LegacyGmInspectionSection.Quests:
                lines.Add($"Quest rows: {session.Character.PlayerQuests.Quests.Count}. Showing at most 9.");
                lines.AddRange(session.Character.PlayerQuests.Quests.Take(9).Select(row => $"Quest {row.Uid}; status {(uint)row.Status}; time {row.Time}; objectives {row.Intentions.Count}."));
                break;
            case LegacyGmInspectionSection.Mail:
                if (_mailboxes is null || !_mailboxes.TryReadGmDiagnostic(snapshot.CharacterUid, out var mails))
                {
                    lines.Add("Mailbox cache unavailable; no file load or cache mutation was attempted.");
                    break;
                }
                lines.Add($"Cached live mails: {mails.Length}. Showing at most 9; saved mail is not verified.");
                lines.AddRange(mails.Take(9).Select(row => $"Mail {row.MailId}; attachment UID {row.AttachedItemUid}; gold {row.Gold}; CPs {row.ConquerPoints}; package {row.PackageFlag}."));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(section));
        }
        return lines.ToArray();
    }

    private static string Optional(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "unavailable";
}
