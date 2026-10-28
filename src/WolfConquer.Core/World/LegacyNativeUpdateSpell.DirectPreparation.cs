using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeUpdateSpell
{
    private sealed partial class TrainingPlan
    {
        private readonly LegacyNativePacket stream;
        private readonly LegacyNativeGameClientRuntime client;
        private readonly LegacyNativePlayerRuntime player;
        private readonly LegacyNativeInteractQuery attack;
        private readonly uint uid, damage, right, left, spellRate;
        private readonly ushort spellId, attackType, playerLevel, originalLevel;
        private readonly LegacyNativeEquipItemStorage equipment;
        private readonly LegacyNativeProficiencyStorage? profs;
        private readonly LegacyNativeSpellStorage? storage;
        private readonly LegacyNativeSecuritySocket? socket;
        private readonly bool fake;
        private readonly LegacyNativeSpellStorage.DirectSpellSnapshot.DirectSpellChange? selected;
        private readonly Dictionary<ushort, LegacyNativeMagicType.Magic>? metadata;
        private LegacyNativeMagicType.Magic? row;
        private int metadataCount;
        private uint threshold;
        private ushort needLevel;
        private bool rune, leveled;
        private byte[]? frame;
        internal bool HasSpell => selected is not null;
        internal bool HasProgress => frame is not null;

        private TrainingPlan(LegacyNativePacket stream, LegacyNativeGameClientRuntime client, LegacyNativePlayerRuntime player,
            LegacyNativeInteractQuery attack, uint damage, Dictionary<ushort, LegacyNativeMagicType.Magic>? metadata,
            LegacyNativeEquipItemStorage equipment, LegacyNativeProficiencyStorage? profs, LegacyNativeSpellStorage? storage,
            LegacyNativeSpellStorage.DirectSpellSnapshot.DirectSpellChange? selected)
        {
            this.stream = stream; this.client = client; this.player = player; this.attack = attack; this.damage = damage;
            this.metadata = metadata; this.equipment = equipment; this.profs = profs; this.storage = storage; this.selected = selected;
            socket = client.Socket; fake = client.Fake;
            uid = player.UID; playerLevel = player.Level; spellId = attack.SpellID; attackType = attack.AtkType;
            right = equipment.RightWeapon / 1000; left = equipment.LeftWeapon / 1000;
            spellRate = LegacyNativeServerConfig.ExpRateSpell; originalLevel = selected?.Value.Level ?? 0;
        }

        internal static TrainingPlan? TryPrepare(LegacyNativePacket stream, LegacyNativeGameClientRuntime client,
            LegacyNativeInteractQuery attack, uint damage, Dictionary<ushort, LegacyNativeMagicType.Magic>? metadata, bool physical)
        {
            TrainingPlan plan;
            lock (client.SpellAdmissionSyncRoot)
            {
                var player = client.Player;
                if (player is null) return null;
                lock (player.SpellAdmissionSyncRoot)
                {
                    LegacyNativeSpellStorage.RequireDirectEntry(client);
                    if (!ReferenceEquals(player.Owner, client) || player.UID == 0) throw new InvalidOperationException("Training requires its physical player.");
                    var storage = metadata is null ? null : client.MySpells;
                    if (metadata is not null && storage is null) return null;
                    LegacyNativeSpellStorage.DirectSpellSnapshot.DirectSpellChange? selected = null;
                    if (storage is not null)
                        lock (storage.MutationGate) selected = storage.CaptureDirectSnapshot(client, player).Find(attack.SpellID);
                    if (selected is null && !physical || client.Equipment is not { } equipment || stream is null) return null;
                    if (!equipment.IsOwnedBy(client))
                        throw new InvalidOperationException("Training requires its physical equipment owner.");
                    var profs = client.MyProfs;
                    if (equipment.RightWeapon / 1000 != 0 || equipment.LeftWeapon / 1000 != 0)
                    {
                        if (profs is null) return null;
                        if (!profs.IsOwnedBy(client))
                            throw new InvalidOperationException("Training requires its physical proficiency owner.");
                    }
                    plan = new(stream, client, player, attack, damage, metadata, equipment, profs, storage, selected);
                    if (storage is not null)
                        lock (storage.MutationGate)
                        {
                            if (!plan.PrepareMetadata()) return null;
                            selected?.RequireCurrent();
                        }
                }
            }
            return plan.PrepareProgress() ? plan : null;
        }

        private bool PrepareMetadata()
        {
            if (selected is null || metadata is null) return true;
            metadataCount = metadata.Count;
            // Retained progression gap: Count-1 assumes contiguous zero-based
            // level keys. The Magic loader inserts declared levels without
            // proving that relation; this coordination repair does not fix it.
            if (originalLevel >= metadataCount - 1) return true;
            if (!metadata.TryGetValue(originalLevel, out row) || row is null ||
                row.ID != spellId || row.Level != originalLevel) return false;
            threshold = row.Experience; needLevel = row.NeedLevel; rune = row.isRuneSkill;
            return true;
        }

        private bool PrepareProgress()
        {
            if (selected is null || row is null || rune || playerLevel < needLevel) return true;
            var value = selected.Value;
            if (!TryPrepareSpellExperience(value.Experience, damage, spellRate, out var experience)) return false;
            ushort level = originalLevel;
            if ((uint)experience > threshold) { level = checked((ushort)(level + 1)); experience = 0; }
            if (value.PreviousLevel != 0 && value.PreviousLevel >= level / 2) level = value.PreviousLevel;
            selected.PrepareProgress(experience, level);
            value.Experience = experience; value.Level = level;
            leveled = level > originalLevel;
            frame = LegacyNativeSpellPacket.PrepareSpellFrame(value, LegacyNativePreparedPacketFrames.CaptureSeal());
            LegacyNativePreparedPacketStorage.Validate(stream, frame.Length);
            return true;
        }
    }
}
