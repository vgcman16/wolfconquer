using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed record WorldSession(
    long AccountId,
    string Username,
    CharacterSnapshot Character,
    PlayerItemSnapshot PlayerItems,
    PlayerSpellSnapshot PlayerSpells,
    PlayerProficiencySnapshot PlayerProficiencies,
    bool AlternateEquipmentActive = false,
    uint? InteractionPartnerUid = null,
    uint ActiveInteractionDance = 0,
    CombatGearSnapshot? CombatGear = null,
    string? UsersRoot = null,
    uint? ActiveNpcId = null,
    uint? PendingNobilityDonationConquerPoints = null,
    DateTimeOffset? LastLegacyPotionUseUtc = null,
    bool LegacyTradeActive = false,
    IReadOnlyDictionary<ushort, DateTimeOffset>? MagicCooldowns = null,
    ushort LegacyRandomSpell = 0,
    uint? PendingBankConquerPoints = null,
    bool PendingBankConquerPointsIsDeposit = false,
    IReadOnlyDictionary<uint, uint>? LegacyExchangeShopBuyCounts = null,
    IReadOnlyDictionary<uint, uint>? LegacyExchangeShopGoodsExCounts = null,
    uint LegacyCatalogSelectedItemId = 0,
    DateTimeOffset? LegacyVoteClaimDueAtUtc = null,
    string LegacyAnimaSecurityPendingPin = "",
    bool LegacyAnimaSecurityOldPinVerified = false,
    LegacyVendorListingBook? LegacyVendorListings = null,
    LegacyPhysicalStatusOverlay? PhysicalStatusOverlay = null,
    LegacyFineRainEquipmentAdmission? FineRainEquipmentAdmission = null,
    LegacyEquipmentHitpointsStatus? EquipmentHitpointsStatus = null)
{
    private CharacterSnapshot _character = Character;
    private LegacyCanAttackFlashingRegistration? _canAttackFlashingRegistration;

    // Keep constructor/deconstruction shape while retiring only this transient owned registration.
    public CharacterSnapshot Character
    {
        get => _character;
        init
        {
            _character = value;
            if (_canAttackFlashingRegistration is { } registration && !registration.IsValidFor(value))
                _canAttackFlashingRegistration = null;
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    internal LegacyCanAttackFlashingRegistration? CanAttackFlashingRegistration
    {
        get => _canAttackFlashingRegistration;
        init => _canAttackFlashingRegistration =
            value is not null && value.IsValidFor(_character) ? value : null;
    }
}
