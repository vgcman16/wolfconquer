using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class SqlWorldStateBundleTests
{
    [Fact]
    public void InvalidCoreFundsCannotBePromotedIntoACompleteBundle()
    {
        Assert.False(SqlWorldStateImage.TryCreate(501, 9001, SqlWorldStateTestData.Character(9001, money: -1),
            PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty), out var core, out var coreFailure));
        Assert.Null(core); Assert.Equal(SqlWorldStateFailure.InvalidFunds, coreFailure);
        Assert.False(SqlWorldStateBundle.TryCreate(core, Missing(), out var bundle, out var failure));
        Assert.Null(bundle); Assert.Equal(SqlWorldStateBundleFailure.InvalidCore, failure);
    }

    [Fact]
    public void InvalidCoreShapeKindPresenceAndPresenceBytesNeverPublishABundle()
    {
        Assert.False(SqlWorldStateBundle.TryCreate(null, Missing(), out var bundle, out var failure));
        Assert.Null(bundle); Assert.Equal(SqlWorldStateBundleFailure.InvalidCore, failure);
        Refuses(Missing()[..3], SqlWorldStateBundleFailure.SidecarCount);
        Refuses(Missing().Append(Missing()[0]).ToArray(), SqlWorldStateBundleFailure.SidecarCount);
        var unknown = Missing(); unknown[0] = new((SqlWorldStateSidecarKind)0, SqlWorldStateSidecarPresence.Missing, ReadOnlyMemory<byte>.Empty);
        Refuses(unknown, SqlWorldStateBundleFailure.InvalidKind);
        var repeated = Missing(); repeated[3] = repeated[0]; Refuses(repeated, SqlWorldStateBundleFailure.DuplicateKind);
        Refuses(Replace(SqlWorldStateSidecarKind.Spells, [], (SqlWorldStateSidecarPresence)2), SqlWorldStateBundleFailure.InvalidPresence);
        Refuses(Replace(SqlWorldStateSidecarKind.Spells, [], SqlWorldStateSidecarPresence.Present), SqlWorldStateBundleFailure.PresenceBytes);
        Refuses(Replace(SqlWorldStateSidecarKind.Spells, new byte[4], SqlWorldStateSidecarPresence.Missing), SqlWorldStateBundleFailure.PresenceBytes);
    }

    [Fact]
    public void ActualSpellAndProficiencyMembershipGuardsRefuseMalformedOrDuplicateCompleteImages()
    {
        var zeroId = Spells(); zeroId[4] = zeroId[5] = 0;
        var negativeExperience = Spells(); Array.Fill(negativeExperience, (byte)0xFF, 8, 4);
        var duplicateSpell = Spells(); duplicateSpell[16] = duplicateSpell[4]; duplicateSpell[17] = duplicateSpell[5];
        foreach (var bytes in new[] { zeroId, negativeExperience, duplicateSpell, Spells()[..^1], Spells().Append((byte)0).ToArray(), new byte[] { 0xFF, 0xFF, 0xFF, 0x7F } })
            Refuses(Replace(SqlWorldStateSidecarKind.Spells, bytes), SqlWorldStateBundleFailure.InvalidSpells);
        var duplicateProficiency = Proficiencies(); Array.Copy(duplicateProficiency, 4, duplicateProficiency, 24, 4);
        foreach (var bytes in new[] { duplicateProficiency, Proficiencies()[..^1], Proficiencies().Append((byte)0).ToArray(), new byte[] { 0xFF, 0xFF, 0xFF, 0xFF } })
            Refuses(Replace(SqlWorldStateSidecarKind.Proficiencies, bytes), SqlWorldStateBundleFailure.InvalidProficiencies);
        // Native admission checks duplicate IDs, not a guessed proficiency-ID-zero gameplay rule.
        var proficiencyZero = Proficiencies(); Array.Clear(proficiencyZero, 4, 4);
        Assert.Equal(proficiencyZero, Sidecar(Bundle(inputs: Replace(SqlWorldStateSidecarKind.Proficiencies, proficiencyZero)),
            SqlWorldStateSidecarKind.Proficiencies).CopyBytes());
    }

    [Fact]
    public void ActualQuestFullWalkRefusesLateTruncationImpossibleIntentionsAndTrailingBytes()
    {
        var negativeIntentions = Quests(); Array.Fill(negativeIntentions, (byte)0xFF, 16, 4);
        var hugeIntentions = Quests(); hugeIntentions[16] = hugeIntentions[17] = hugeIntentions[18] = 0xFF; hugeIntentions[19] = 0x7F;
        foreach (var bytes in new[] { Quests()[..^1], Quests().Append((byte)0).ToArray(), negativeIntentions, hugeIntentions,
            new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, new byte[] { 0xFF, 0xFF, 0xFF, 0x7F } })
            Refuses(Replace(SqlWorldStateSidecarKind.Quests, bytes), SqlWorldStateBundleFailure.InvalidQuests);
    }

    [Fact]
    public void CombatGearUsesTheCompleteCanonicalAdmissionValidatorRatherThanPermissiveParserDefaults()
    {
        var gear = EmptyGear();
        Assert.True(LegacyTradeJournal.TryReadCompleteMailboxGear(gear, out var parsed));
        Assert.Empty(Assert.IsType<CombatGearSnapshot>(parsed).Items);
        Assert.Equal(gear, Sidecar(Bundle(inputs: Replace(SqlWorldStateSidecarKind.CombatGear, gear)), SqlWorldStateSidecarKind.CombatGear).CopyBytes());
        var nonempty = new CombatGearSnapshot(11, 22, 33, 12, 1, new(9, 8, 7, 6, 5, 4, 3),
            [new CombatGearSnapshotItem(701, 4, 123, 1, 2, 928000,
                [new uint[] { 101, 102, 103, 104, 105, 106, 107 }, new uint[] { 201, 202, 203, 204, 205, 206, 207 }],
                new uint[] { 11, 12, 13, 14, 15, 16 })], new(2, new uint[] { 301, 302 }));
        var fullBytes = Encoding.UTF8.GetBytes(CombatGearSnapshotLoader.ToLegacyText(nonempty));
        var retained = Sidecar(Bundle(inputs: Replace(SqlWorldStateSidecarKind.CombatGear, fullBytes)), SqlWorldStateSidecarKind.CombatGear).CopyBytes();
        Assert.Equal(fullBytes, retained); Assert.True(LegacyTradeJournal.TryReadCompleteMailboxGear(retained, out var complete));
        var actual = Assert.IsType<CombatGearSnapshot>(complete);
        Assert.Equal(11U, actual.MasteryPoints); Assert.Equal(1, actual.Count); Assert.Equal(701U, Assert.Single(actual.Items).ItemID);
        Assert.Equal(new uint[] { 201, 202, 203, 204, 205, 206, 207 }, actual.Items[0].Animas[1]);
        Assert.Equal(new uint[] { 11, 12, 13, 14, 15, 16 }, actual.Items[0].BlackJades);
        Assert.Equal(new uint[] { 301, 302 }, actual.BlackJadeBag.BlackJades);
        var text = Encoding.UTF8.GetString(gear);
        var incomplete = Encoding.UTF8.GetBytes(text.Replace("MasteryPoints=0" + Environment.NewLine, "", StringComparison.Ordinal));
        // The ordinary parser defaults this field; complete admission must still refuse the lost bytes.
        Assert.Equal(0U, CombatGearSnapshotLoader.Parse(Encoding.UTF8.GetString(incomplete)).MasteryPoints);
        foreach (var bytes in new[] { incomplete, gear[..^1], gear.Append((byte)0xFF).ToArray(),
            Encoding.UTF8.GetBytes(text.Replace("Lottery=0" + Environment.NewLine + "Count=0", "Lottery=0" + Environment.NewLine + "Count=1", StringComparison.Ordinal)),
            Encoding.UTF8.GetBytes(text.Replace("[BlackJadeBag]" + Environment.NewLine + "Count=0", "[BlackJadeBag]" + Environment.NewLine + "Count=1", StringComparison.Ordinal)) })
            Refuses(Replace(SqlWorldStateSidecarKind.CombatGear, bytes), SqlWorldStateBundleFailure.InvalidCombatGear);
    }
}
