using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeUpdateSpell
{
    private sealed partial class TrainingPlan
    {
        private bool HasBinding() => ReferenceEquals(client.Player, player) && ReferenceEquals(player.Owner, client) && player.UID == uid &&
            player.Level == playerLevel && attack.SpellID == spellId && attack.AtkType == attackType &&
            ReferenceEquals(client.Equipment, equipment) && equipment.IsOwnedBy(client) &&
            (right == 0 && left == 0 || ReferenceEquals(client.MyProfs, profs) && profs?.IsOwnedBy(client) == true) &&
            ReferenceEquals(client.Socket, socket) && client.Fake == fake &&
            equipment.RightWeapon / 1000 == right && equipment.LeftWeapon / 1000 == left;

        private bool HasMetadata()
        {
            if (metadata is null || selected is null) return true;
            if (metadata.Count != metadataCount) return false;
            if (row is null) return originalLevel >= metadataCount - 1;
            foreach (var pair in metadata)
                if (pair.Key == originalLevel)
                    return ReferenceEquals(pair.Value, row) && row.ID == spellId && row.Level == originalLevel &&
                        row.Experience == threshold && row.NeedLevel == needLevel && row.isRuneSkill == rune;
            return false;
        }

        internal bool IsCurrent()
        {
            LegacyNativeSpellStorage.RequireDirectEntry(client);
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
            {
                LegacyNativeSpellStorage.RequireDirectEntry(client);
                if (!HasBinding() || !HasMetadata()) return false;
                if (storage is null) return true;
                lock (storage.MutationGate)
                {
                    if (!ReferenceEquals(client.MySpells, storage) || !storage.IsOwnedBy(client)) return false;
                    if (selected is null) return true;
                    if (committed) return selected.MatchesAfter();
                    try { selected.RequireCurrent(); return true; }
                    catch (InvalidOperationException) { return false; }
                }
            }
        }

        internal bool CommitProgress()
        {
            LegacyNativeSpellStorage.RequireDirectEntry(client);
            lock (client.SpellAdmissionSyncRoot)
            lock (player.SpellAdmissionSyncRoot)
            {
                LegacyNativeSpellStorage.RequireDirectEntry(client);
                lock (storage!.MutationGate)
                {
                    if (!HasBinding() || !HasMetadata() || LegacyNativeServerConfig.ExpRateSpell != spellRate) return false;
                    try
                    {
                        LegacyNativePreparedPacketStorage.Validate(stream, frame!.Length);
                        if (LegacyNativePreparedPacketStorage.Address(stream) != address) return false;
                        selected!.CommitProgress();
                    }
                    catch (InvalidOperationException) { return false; }
                    committed = true;
                    return true;
                }
            }
        }

        internal void PublishProgress()
        {
            if (leveled)
                try { client.SendSysMesage("You increased the spell level!"); }
                catch (Exception error) { LegacyNativeConsole.WriteLine(error.ToString()); }
            if (!IsCurrent()) return;
            try
            {
                if (LegacyNativePreparedPacketStorage.Address(stream) != address) return;
                LegacyNativePreparedPacketStorage.Write(stream, frame!);
                client.Send(stream);
            }
            catch (InvalidOperationException) { /* Committed progress; notification unavailable. */ }
        }

        internal void UpdateProficiencies()
        {
            if (profs is null) return;
            if (right != 0)
            {
                if (!IsCurrent()) return;
                profs.CheckUpdate(right, damage * LegacyNativeServerConfig.ExpRateProf, stream);
            }
            if (left != 0)
            {
                if (!IsCurrent()) return;
                profs.CheckUpdate(left, damage * LegacyNativeServerConfig.ExpRateProf, stream);
            }
        }
    }
}
