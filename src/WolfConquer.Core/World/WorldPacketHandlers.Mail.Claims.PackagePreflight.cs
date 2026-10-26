using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static bool TryPrepareMailboxPackage(LegacyMailboxClaimBaseline baseline, LegacyMailboxModePacket packet,
        out MailboxPackagePrepared prepared, out string reason)
    {
        prepared = null!;
        reason = MailboxClaimPrefix + "PackageOwnersUnresolved";
        var owner = baseline.Owner;
        var expected = owner.Expected;
        if (owner.Context.MailboxPackages is not { } producer ||
            !producer.IsCurrent(expected.UsersRoot, owner.Context.PlayerItemsRoot, owner.Context.Mailboxes!.ClaimRoot,
                owner.Context.TradeRecovery) || owner.Context.WorldData is not { } catalog ||
            expected.CombatGear is null || !MailboxClaimAdmissionCurrent(owner)) return false;
        var all = packet.Type == LegacyMailboxModeType.ClaimAll;
        var selected = SelectMailboxPackageRows(baseline, packet);
        if (!selected.Any(mail => mail.Flag != 0))
        { reason = MailboxClaimPrefix + (selected.Length == 0 ? "MailUnavailable" : "PackageEmpty"); return false; }
        var gearPath = Path.Combine(producer.CombatGearRoot, expected.Character.Uid.ToString(CultureInfo.InvariantCulture) + ".ini");
        if (!CharacterSnapshotFileBaseline.TryReadBytes(gearPath, MailboxClaimPrefix, out var gearBefore, out reason) ||
            !LegacyTradeJournal.TryReadCompleteMailboxGear(gearBefore, out var gear) ||
            !Encoding.UTF8.GetBytes(CombatGearSnapshotLoader.ToLegacyText(expected.CombatGear)).AsSpan().SequenceEqual(gearBefore) ||
            !TryReadMailboxPackageScalar(baseline.Character.Fields, "ClassExperience", out var classBefore) ||
            !TryReadMailboxPackageScalar(expected.Character.Attributes, "ClassExperience", out var liveClass) || classBefore != liveClass ||
            CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(baseline.Character.Bytes, out _),
                expected.Character.SourceFile).Class != expected.Character.Class) return false;
        var steps = new List<MailboxPackageStep>();
        var grants = new List<LegacyMailboxPackageItemRecipe>();
        var metadata = new Dictionary<uint, LegacyMailboxPackageCatalog>();
        var replacements = new Dictionary<uint, LegacyMailboxEntry>();
        var updated = expected;
        uint classAfter = classBefore, ninjaPoints = 0, piratePoints = 0, pirateExp = 0;
        foreach (var original in selected)
        {
            var current = replacements.GetValueOrDefault(original.Id) ?? original;
            var next = current;
            if (all)
            {
                updated = PrepareMailboxClaimMoney(updated, current, []);
                next = next.WithClaimedMoney();
                if (current.ItemId != 0 && !baseline.Items.TryGetWarehouse(LegacyMailboxWarehouseId, current.ItemId, out _))
                { reason = MailboxClaimPrefix + "AttachmentMissing"; return false; }
                next = next.WithClearedItemId();
            }
            var pays = current.Flag != 0;
            if (pays)
            {
                if (!TryPrepareMailboxPackageGrants(catalog, current.Id, current.Flag, expected.Character.Class,
                    grants, metadata, out var exp, out var ninja, out var pirate, out var pirateExperience, out reason)) return false;
                classAfter = checked(classAfter + exp);
                ninjaPoints = checked(ninjaPoints + ninja);
                piratePoints = checked(piratePoints + pirate);
                pirateExp = checked(pirateExp + pirateExperience);
                next = next.WithClearedFlag();
            }
            replacements[original.Id] = next;
            steps.Add(new(current, next, pays, classAfter, ninjaPoints, piratePoints));
        }
        var attachmentSlots = all ? selected.DistinctBy(mail => mail.Id).Count(mail => mail.ItemId != 0) : 0;
        if (grants.Count + attachmentSlots > LegacyInventorySlots - expected.PlayerItems.InventoryItems.Count)
        { reason = MailboxClaimPrefix + "PackageCapacity"; return false; }
        updated = updated with { Character = WithCharacterAttribute(updated.Character, "ClassExperience", classAfter) };
        if (!TryCaptureMailboxPackageNative(owner, baseline.Character.Fields, classBefore, checked(classAfter - classBefore),
            ninjaPoints, piratePoints, pirateExp, gear!, gearBefore, out var native, out reason)) return false;
        if (native.Ninja is { } ninjaProjection)
        {
            updated = updated with { Character = WithMailboxPackageText(updated.Character, "MsgNinja", ninjaProjection.AfterText) };
            var initial = checked(ninjaProjection.AfterExp - ninjaPoints);
            for (var index = 0; index < steps.Count; index++)
                steps[index] = steps[index] with { NinjaAfter = checked(initial + steps[index].NinjaAfter) };
        }
        var gearAfter = native.Pirate?.CopyAfterImage() ?? gearBefore.ToArray();
        var gearSnapshot = native.Pirate?.CopyAfterSnapshot() ?? gear!;
        updated = updated with { CombatGear = gearSnapshot };
        var mailAfter = baseline.Mail.ProjectPackages(replacements);
        if (!TryProjectMailboxPackageCharacter(baseline, updated, out var characterAfter))
        { reason = MailboxClaimPrefix + "PackageCharacterInvalid"; return false; }
        prepared = new(baseline, producer, selected, steps.ToArray(), grants.ToArray(), metadata.Values.ToArray(),
            updated, mailAfter, gearPath, gearBefore, gearAfter, characterAfter,
            native.Class, native.Ninja, native.Pirate, native.SendClassExperience);
        reason = string.Empty;
        return true;
    }

    private static bool TryReadMailboxPackageScalar(IReadOnlyDictionary<string, string> fields, string key, out uint value)
    {
        value = 0;
        return fields.TryGetValue(key, out var raw) && uint.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static CharacterSnapshot WithMailboxPackageText(CharacterSnapshot character, string key, string value)
    {
        var fields = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase) { [key] = value };
        return character with { Attributes = fields };
    }
}
