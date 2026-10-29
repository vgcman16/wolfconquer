using System.Buffers.Binary;
using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class LegacyOwnedPlayerInspection
{
    private const int MaximumSummaryRows = 200;

    public static bool TryInspect(string configurationPath, uint accountId,
        out LegacyOwnedPlayerInspectionReport? report, out string reason)
    {
        report = null;
        reason = "ownedPlayerInspectionRequestInvalid";
        var started = DateTimeOffset.UtcNow;
        try
        {
            if (accountId == 0 || !TryOwnedInspectionPath(configurationPath, out var root)) return false;
            var configuration = OwnedPlayerFileObservation.Read("Configuration", configurationPath,
                LegacyOwnedAdmissionOptions.MaximumConfigurationBytes, required: true);
            if (!LegacyOwnedAdmissionOptions.TryReadFile(configurationPath, out var options, out _) || options is null ||
                !InspectionRoutesContained(root, options))
            { reason = "ownedPlayerInspectionConfigurationInvalid"; return false; }
            if (LegacyTradeJournal.HasPending(options.UsersRoot))
            { reason = "ownedPlayerInspectionUnstable:pendingTradeJournal"; return false; }
            var loader = options.CreateSnapshotLoader();
            if (!string.Equals(Path.GetFullPath(loader.UsersRoot), options.UsersRoot,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            { reason = "ownedPlayerInspectionLoaderRouteMismatch"; return false; }
            var accountLeaf = accountId.ToString(CultureInfo.InvariantCulture) + ".ini";
            var characterFile = OwnedPlayerFileObservation.Read("Character", Path.Combine(loader.UsersRoot, accountLeaf),
                CharacterSnapshotFileBaseline.MaximumBytes, required: true);
            // Public LoadByAccountId may persist expired nobility. Decode the
            // bounded actual saved bytes instead; no expiry restore/save occurs.
            var character = CharacterSnapshotParser.ParseText(
                CharacterSnapshotFileEncoding.Decode(characterFile.Bytes!, out _), accountLeaf);
            if (character.Uid is <= 0 or > uint.MaxValue || character.Money < 0 || character.ConquerPoints < 0)
            { reason = "ownedPlayerInspectionSnapshotInvalid"; return false; }
            var leaf = character.Uid.ToString(CultureInfo.InvariantCulture) + ".bin";
            var before = new[]
            {
                configuration, characterFile,
                OwnedPlayerFileObservation.Read("Items", Path.Combine(loader.PlayerItemsRoot!, leaf), CharacterSnapshotFileBaseline.MaximumBytes),
                OwnedPlayerFileObservation.Read("Spells", Path.Combine(loader.PlayerSpellsRoot!, leaf), CharacterSnapshotFileBaseline.MaximumBytes),
                OwnedPlayerFileObservation.Read("Proficiencies", Path.Combine(loader.PlayerProficienciesRoot!, leaf), CharacterSnapshotFileBaseline.MaximumBytes),
                OwnedPlayerFileObservation.Read("Mailbox", Path.Combine(options.MailboxRoot, leaf), CharacterSnapshotFileBaseline.MaximumBytes),
            };
            var items = before[2].Bytes is { } itemBytes ? PlayerItemSnapshotLoader.Parse(itemBytes) : PlayerItemSnapshot.Empty;
            if (!LegacyItemSaveGraph.TryCapture(items, out _))
            { reason = "ownedPlayerInspectionSnapshotInvalid"; return false; }
            var spells = ReadInspectionSpells(before[3].Bytes);
            var proficiencies = ReadInspectionProficiencies(before[4].Bytes);
            // A fresh detached reader uses the actual complete saved-mail decoder.
            // Load changes only this reader's local cache, never the running Frame.
            var mailReader = new LegacyMailboxStore(Directory.GetParent(options.MailboxRoot)!.FullName);
            if (!string.Equals(Path.GetFullPath(mailReader.GetMailboxPath(checked((uint)character.Uid))),
                Path.GetFullPath(before[5].Path), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            { reason = "ownedPlayerInspectionMailboxRouteInvalid"; return false; }
            var mails = mailReader.Load(checked((uint)character.Uid));
            var summaries = SummarizeInspectionItems(items, out var physicalCount);
            var inbox = items.WarehouseItems.Where(item => item.WarehouseId == 1000).Select(item => item.Uid).ToHashSet();
            var shownMails = mails.Take(Math.Max(0, MaximumSummaryRows - summaries.Length)).Select(mail =>
                new LegacyOwnedPlayerMail(mail.Id, mail.ItemId, mail.ItemId != 0 && inbox.Contains(mail.ItemId),
                    mail.Money, mail.EMoney, mail.Flag)).ToArray();
            OwnedPlayerFileObservation[] after;
            try
            {
                after = before.Select(file => OwnedPlayerFileObservation.Read(file.Kind, file.Path,
                    file.Kind == "Configuration" ? LegacyOwnedAdmissionOptions.MaximumConfigurationBytes :
                        CharacterSnapshotFileBaseline.MaximumBytes)).ToArray();
            }
            catch (Exception error) when (LegacyOwnedAdmissionOptions.IsConfigurationFailure(error))
            { throw new OwnedPlayerInspectionUnstableException(); }
            if (LegacyTradeJournal.HasPending(options.UsersRoot) || before.Where((file, i) => !file.SameImage(after[i])).Any())
            { reason = "ownedPlayerInspectionUnstable:selectedFilesChanged"; return false; }
            report = new(1, options.ExpectedDomain, accountId, null, "notStoredInOwnedAdmissionConfiguration",
                new(character.Uid, character.MapId, character.X, character.Y, character.Body, character.Class,
                    character.Level, character.Money, character.ConquerPoints,
                    ReadInspectionSavedVital(character, "MinHitPoints"), ReadInspectionSavedVital(character, "MinMana"),
                    ReadInspectionSavedVital(character, "MaxMana")),
                items.InventoryItems.Count, items.EquipmentItems.Count, items.WarehouseItems.Count, physicalCount,
                spells.Spells.Count, proficiencies.Proficiencies.Count, mails.Count, summaries, shownMails,
                physicalCount > summaries.Length || mails.Count > shownMails.Length, started, DateTimeOffset.UtcNow,
                "selectedFileBytesAndMetadataEqualAtReadBoundaries;crossProcessChangesAndRestoreABAOrLiveStoreDriftNotExcluded",
                false, before.Select(file => file.Evidence()).ToArray());
            reason = string.Empty;
            return true;
        }
        catch (OwnedPlayerInspectionUnstableException)
        { reason = "ownedPlayerInspectionUnstable:selectedFilesChanged"; return false; }
        catch (Exception error) when (LegacyOwnedAdmissionOptions.IsConfigurationFailure(error))
        { reason = "ownedPlayerInspectionSnapshotUnreadableOrInvalid"; return false; }
    }

    // Preserve absence/invalid optional saved text, especially MaxMana whose
    // runtime parser otherwise defaults to MinMana. No additional file reads.
    private static long? ReadInspectionSavedVital(CharacterSnapshot character, string key) =>
        character.Attributes.TryGetValue(key, out var text) &&
        long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static bool TryOwnedInspectionPath(string path, out string root)
    {
        root = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
            Path.GetFileName(path) != LegacyOwnedAdmissionFixture.ConfigurationFileName) return false;
        root = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var name = Path.GetFileName(root);
        return name.StartsWith(LegacyOwnedAdmissionFixture.DirectoryPrefix, StringComparison.Ordinal) &&
            Guid.TryParseExact(name[LegacyOwnedAdmissionFixture.DirectoryPrefix.Length..], "N", out var id) && id != Guid.Empty;
    }

    private static bool InspectionRoutesContained(string root, LegacyOwnedAdmissionOptions options) =>
        new[] { options.UsersRoot, options.ItemsRoot, options.SpellsRoot, options.ProficienciesRoot, options.QuestsRoot,
            options.CombatGearRoot, options.MailboxRoot, options.RedeemFile, options.ClaimFile }.All(path =>
        {
            var relative = Path.GetRelativePath(root, path);
            return relative != "." && relative != ".." && !Path.IsPathFullyQualified(relative) &&
                !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        });

    private static PlayerSpellSnapshot ReadInspectionSpells(byte[]? bytes)
    {
        if (bytes is null) return PlayerSpellSnapshot.Empty;
        RequireCountedImage(bytes, PlayerSpellSnapshotLoader.LegacySpellSize);
        var parsed = PlayerSpellSnapshotLoader.Parse(bytes);
        if (parsed.Spells.Any(row => row.Id == 0) || parsed.Spells.Select(row => row.Id).Distinct().Count() != parsed.Spells.Count)
            throw new InvalidDataException();
        return parsed;
    }

    private static PlayerProficiencySnapshot ReadInspectionProficiencies(byte[]? bytes)
    {
        if (bytes is null) return PlayerProficiencySnapshot.Empty;
        RequireCountedImage(bytes, PlayerProficiencySnapshotLoader.LegacyProficiencySize);
        var parsed = PlayerProficiencySnapshotLoader.Parse(bytes);
        if (parsed.Proficiencies.Any(row => row.Id == 0) || parsed.Proficiencies.Select(row => row.Id).Distinct().Count() != parsed.Proficiencies.Count)
            throw new InvalidDataException();
        return parsed;
    }

    private static void RequireCountedImage(byte[] bytes, int size)
    {
        if (bytes.Length < sizeof(int)) throw new InvalidDataException();
        var count = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (count < 0 || sizeof(int) + (long)count * size != bytes.Length) throw new InvalidDataException();
    }
}
