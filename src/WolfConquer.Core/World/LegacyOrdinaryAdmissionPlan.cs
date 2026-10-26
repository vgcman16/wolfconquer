using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyOrdinaryAdmissionPlan
{
    private readonly WorldServerFrameHandler frame;
    private readonly ConquerTcpConnection connection;
    private readonly LegacyNativeGameClientRuntime? native;
    private readonly LegacyNativePlayerRuntime? player;
    private readonly LegacyNativeEquipmentRuntime? previousEquipment, preparedEquipment;
    private readonly LegacyNativeExistingCharacterPrefix? prefix;
    private readonly Dictionary<LegacyTradeJournalFileKind, byte[]?> images;
    private readonly LegacyOrdinaryAdmissionRoots roots;
    private readonly CharacterSnapshot source, baseCharacter;
    private readonly PlayerItemSnapshot savedItems;
    private readonly PlayerSpellSnapshot savedSpells;
    private readonly PlayerProficiencySnapshot profs;
    private readonly CombatGearSnapshot gear;
    private readonly LegacyOrdinaryNormalization normalization;
    private readonly LegacySecondaryPasswordState pin;
    private readonly LegacyOrdinaryAdmissionUidOwner? uids;
    internal LegacyTradeRecoveryState Recovery { get; }
    internal LegacySpellPublicationPlan LoginSpellPublication { get; }
    internal WorldSession Baseline { get; }
    internal WorldSession? Candidate { get; private set; }
    private readonly long account;
    private readonly string username;
    private readonly byte[] characterAfter, spellAfter;
    private readonly byte[] ruleItemsImage, profsImage, questImage;
    private readonly string gearText;
    private bool committed, published;
    private WorldSessionStore.OrdinaryAdmissionLease? storeLease;
    private readonly DateTime preparedAt;
    private LegacyTradeJournalChange[]? preparedChanges;

    private LegacyOrdinaryAdmissionPlan(WorldServerFrameHandler frame, ConquerTcpConnection connection, long account,
        string username, LegacyOrdinaryAdmissionRoots roots, Dictionary<LegacyTradeJournalFileKind, byte[]?> images,
        CharacterSnapshot source, CharacterSnapshot baseCharacter, PlayerItemSnapshot items, PlayerSpellSnapshot spells,
        PlayerProficiencySnapshot profs, CombatGearSnapshot gear, LegacyOrdinaryNormalization normalization,
        LegacySecondaryPasswordState pin, LegacyNativeExistingCharacterPrefix? prefix,
        LegacyNativeEquipmentRuntime? equipment, LegacyOrdinaryAdmissionUidOwner? uids, LegacyTradeRecoveryState recovery,
        DateTime preparedAt)
    {
        this.frame = frame; this.connection = connection; this.account = account; this.username = username;
        this.roots = roots; this.images = images; this.source = source; this.baseCharacter = baseCharacter;
        savedItems = items; savedSpells = spells; this.profs = profs; this.gear = gear; this.normalization = normalization;
        this.pin = pin; this.prefix = prefix; preparedEquipment = equipment; this.uids = uids; Recovery = recovery;
        this.preparedAt = preparedAt;
        var newLoginSpells = LegacyClassOwnedSpellGrantProfile.GetSpellIds(source)
            .Where(id => !savedSpells.ContainsSpell(id)).ToHashSet();
        if (!LegacySpellPublicationPlan.TryPrepare(normalization.Spells, newLoginSpells, out var publication) || publication is null)
            throw new InvalidDataException("Ordinary admission spell publication is invalid.");
        LoginSpellPublication = publication;
        native = connection.NativeGameClient; player = native?.Player; previousEquipment = native?.RawEquipmentObservation;
        characterAfter = LegacyOrdinaryAdmissionImages.Character(images[LegacyTradeJournalFileKind.Character]!, source, normalization.Character);
        spellAfter = LegacyOrdinaryAdmissionSpellImages.Project(source, images.GetValueOrDefault(LegacyTradeJournalFileKind.Spells));
        ruleItemsImage = PlayerItemSnapshotLoader.ToLegacyBytes(normalization.RuleItems);
        profsImage = PlayerProficiencySnapshotLoader.ToLegacyBytes(profs);
        questImage = PlayerQuestSnapshotLoader.Serialize(baseCharacter.PlayerQuests);
        gearText = CombatGearSnapshotLoader.ToLegacyText(gear);
        Baseline = new(account, username, source, items, spells, profs, CombatGear: gear, UsersRoot: roots.Users)
        { NativeGameClient = native };
    }

    internal bool IsCurrent()
    {
        if (published || committed || !Recovery.IsReadyForUsers(roots.Users) || !EntryCurrent() ||
            !LegacyTradeJournal.AdmissionInputsAreCurrent(roots, account, images)) return false;
        var current = frame.PrepareOrdinaryNormalization(baseCharacter, savedItems, savedSpells, gear);
        return current.NeedsFreshItemIds == normalization.NeedsFreshItemIds &&
            PlayerItemSnapshotLoader.ToLegacyBytes(current.RuleItems).AsSpan()
                .SequenceEqual(ruleItemsImage) &&
            PlayerSpellSnapshotLoader.ToLegacyBytes(current.Spells).AsSpan()
                .SequenceEqual(PlayerSpellSnapshotLoader.ToLegacyBytes(normalization.Spells)) &&
            LegacyOrdinaryAdmissionSpellImages.Project(source, images.GetValueOrDefault(LegacyTradeJournalFileKind.Spells))
                .AsSpan().SequenceEqual(spellAfter) &&
            PlayerProficiencySnapshotLoader.ToLegacyBytes(profs).AsSpan().SequenceEqual(profsImage) &&
            PlayerQuestSnapshotLoader.Serialize(baseCharacter.PlayerQuests).AsSpan().SequenceEqual(questImage) &&
            CombatGearSnapshotLoader.ToLegacyText(gear) == gearText &&
            LegacyOrdinaryAdmissionImages.Character(images[LegacyTradeJournalFileKind.Character]!, source, current.Character)
                .AsSpan().SequenceEqual(characterAfter) && Recovery.IsReadyForUsers(roots.Users) &&
            !normalization.NeedsFreshItemIds && (uids is null || uids.IsCurrent(roots)) && EntryCurrent();
    }

    private bool EntryCurrent() => ReferenceEquals(connection.NativeGameClient, native) &&
        (native is null || ReferenceEquals(native.Player, player) && ReferenceEquals(player!.Owner, native) &&
            ReferenceEquals(native.RawEquipmentObservation, previousEquipment)) &&
        (prefix is null || prefix.IsCurrentForAdmission());

    internal bool TryCommitDurable(WorldSessionStore.OrdinaryAdmissionLease lease, out string reason,
        LegacyOrdinaryAdmissionInterruption interruption = LegacyOrdinaryAdmissionInterruption.None)
    {
        reason = "ordinaryAdmissionOwnerChanged";
        if (!Enum.IsDefined(interruption)) { reason = "ordinaryAdmissionInterruptionInvalid"; return false; }
        if (interruption is LegacyOrdinaryAdmissionInterruption.AfterUidIntent or LegacyOrdinaryAdmissionInterruption.AfterUidReservation)
        { reason = "ordinaryAdmissionHasNoUidAllocation"; return false; }
        try
        {
            storeLease = lease;
            if (!IsCurrent() || !lease.IsCurrentEmpty(connection)) return false;
            var items = normalization.RuleItems;
            // Every return object/image is prepared before the actual joint writer.
            var changes = CreateChanges();
            var inputs = images.Where(row => changes.All(change => change.Kind != row.Key))
                .Select(row => new LegacyOrdinaryAdmissionInput(row.Key, account, row.Value)).ToArray();
            var stored = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(characterAfter, out _),
                Path.GetFileName(roots.PathFor(LegacyTradeJournalFileKind.Character, account))) with { PlayerQuests = baseCharacter.PlayerQuests };
            Candidate = new(account, username, frame.ProjectOrdinaryStartupTitle(stored), items, normalization.Spells,
                profs, CombatGear: gear, UsersRoot: roots.Users) { NativeGameClient = native };
            if (!EntryCurrent() || !LegacyTradeJournal.AdmissionInputsAreCurrent(roots, account, images) ||
                !lease.IsCurrentEmpty(connection))
            {
                reason = "ordinaryAdmissionOwnerChanged";
                return false;
            }
            preparedChanges = changes;
            using var permit = LegacyOrdinaryAdmissionMutationFence.EnterPreparedCommit(this);
            if (!LegacyTradeJournal.TryCommitOrdinaryAdmission(roots, changes, inputs, preparedAt, out reason, interruption))
            {
                if (reason.StartsWith(LegacyTradeJournal.RecoveryRequired, StringComparison.Ordinal))
                { reason = "ordinaryAdmissionRecoveryRequired:" + reason; Recovery.Block(reason); }
                return false;
            }
            committed = true;
            if (interruption == LegacyOrdinaryAdmissionInterruption.AfterJournalComplete)
            { reason = "ordinaryAdmissionRecoveryRequired:committedBeforePublication"; Recovery.Block(reason); return false; }
            return true;
        }
        catch (Exception error) when (IsPreparationFailure(error))
        {
            reason = (committed ? "ordinaryAdmissionRecoveryRequired:" : "ordinaryAdmissionPreparation:") + error.GetType().Name;
            if (committed) Recovery.Block(reason);
            return false;
        }
    }

    internal void PublishCommittedNative()
    {
        if (!committed || published || Candidate is null || storeLease is null || !storeLease.IsCurrentEmpty(connection))
            throw new InvalidOperationException("Ordinary admission has no current committed publication owner.");
        prefix?.PublishCommittedAdmission();
        if (native is not null && preparedEquipment is not null) native.RawEquipmentObservation = preparedEquipment;
        published = true;
    }

    internal bool InitializeCommittedPin() => Candidate is { } candidate &&
        (native is null ? pin.Password == 0 : LegacySecondaryPasswordRuntime.TryInitialize(connection, candidate, pin));

    internal bool IsPublishedNativeOwnerCurrent() => published && ReferenceEquals(connection.NativeGameClient, native) &&
        (native is null || ReferenceEquals(native.Player, player) && ReferenceEquals(player!.Owner, native) &&
            player.UID == account && ReferenceEquals(native.RawEquipmentObservation, preparedEquipment ?? previousEquipment)) &&
        (prefix is null || prefix.IsPublishedAdmissionOwnerCurrent());

    internal bool MatchesConnection(ConquerTcpConnection expected) => ReferenceEquals(connection, expected);
    internal object? NativeGate => native?.SpellAdmissionSyncRoot;
    internal object? PlayerGate => player?.SpellAdmissionSyncRoot;
}
