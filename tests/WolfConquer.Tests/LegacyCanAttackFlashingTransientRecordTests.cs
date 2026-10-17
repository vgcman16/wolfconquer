using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // These are isolated typed-record controls, NOT earned-gameplay acquisition claims.
    [Fact]
    public void CanAttackFlashingTransientRecordPreservesConstructorDeconstructAndJsonCharacterShape()
    {
        var original = CreateSession();
        var session = new WorldSession(original.AccountId, original.Username, original.Character,
            original.PlayerItems, original.PlayerSpells, original.PlayerProficiencies);
        var (accountId, username, character, items, spells, proficiencies, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _, _) = session;
        Assert.Equal(original.AccountId, accountId);
        Assert.Equal(original.Username, username);
        Assert.Same(original.Character, character);
        Assert.Same(original.PlayerItems, items);
        Assert.Same(original.PlayerSpells, spells);
        Assert.Same(original.PlayerProficiencies, proficiencies);
        Assert.Null(session.CanAttackFlashingRegistration);
        Assert.Equal(session, session with { });
        var root = JsonSerializer.SerializeToElement(session);
        Assert.Equal(29, root.EnumerateObject().Count());
        Assert.Equal(JsonSerializer.Serialize(session.Character), root.GetProperty("Character").GetRawText());
        Assert.False(root.TryGetProperty("CanAttackFlashingRegistration", out _));
        var nullCharacter = session with { Character = null! };
        Assert.Null(nullCharacter.Character);
        Assert.Null(nullCharacter.CanAttackFlashingRegistration);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CanAttackFlashingTransientRecordRejectsBothIncompatibleInitializerOrders(
        bool registrationFirst, bool foreignUid)
    {
        var owned = Flash0UnitOwned();
        var registration = Assert.IsType<LegacyCanAttackFlashingRegistration>(owned.CanAttackFlashingRegistration);
        var incompatible = foreignUid ? owned.Character with { Uid = PlayerUid + 9_000 } :
            Flash0UnitCharacter(owned.Character, active: false);
        var changed = registrationFirst
            ? owned with { CanAttackFlashingRegistration = registration, Character = incompatible }
            : owned with { Character = incompatible, CanAttackFlashingRegistration = registration };
        Assert.Null(changed.CanAttackFlashingRegistration);
        Assert.False(LegacyCanAttackFlashingRegistration.TryRead(changed, out _));
        Assert.Throws<ArgumentException>(() => registration.Install(changed));
    }

    [Fact]
    public void CanAttackFlashingTransientRecordClearThenGenericReAddCannotResurrectOldTimer()
    {
        var owned = Flash0UnitOwned();
        var cleared = owned with { Character = Flash0UnitCharacter(owned.Character, active: false) };
        Assert.Null(cleared.CanAttackFlashingRegistration);
        var readded = cleared with { Character = Flash0UnitCharacter(cleared.Character, active: true) };
        Assert.True(HasLegacyStatusFlag(readded.Character.Attributes, 0));
        Assert.Null(readded.CanAttackFlashingRegistration);
        Assert.False(LegacyCanAttackFlashingRegistration.TryRead(readded, out _));
        Assert.NotNull(owned.CanAttackFlashingRegistration); // Immutable prior snapshot, NOT permission to replay it.
    }

    [Fact]
    public void CanAttackFlashingTransientRecordRetainsActiveZeroWithExplicitFalseDeathPolicy()
    {
        var owned = Flash0UnitOwned();
        var character = LegacyStatusFlagDeathRegistration.CaptureAddSpellFlag(owned.Character, 0, false);
        var retained = owned with { Character = character };
        Assert.True(LegacyStatusFlagDeathRegistration.TryRead(retained.Character, 0, out var removeOnDead));
        Assert.False(removeOnDead);
        Assert.True(HasLegacyStatusFlag(retained.Character.Attributes, 0));
        Assert.Same(owned.CanAttackFlashingRegistration, retained.CanAttackFlashingRegistration);
        Assert.True(LegacyCanAttackFlashingRegistration.TryRead(retained, out _));
    }

    [Fact]
    public void CanAttackFlashingTransientRecordForgetIsIdempotentAndNullCharacterRetires()
    {
        var owned = Flash0UnitOwned();
        var forgotten = LegacyCanAttackFlashingRegistration.Forget(owned);
        Assert.True(HasLegacyStatusFlag(forgotten.Character.Attributes, 0));
        Assert.Null(forgotten.CanAttackFlashingRegistration);
        Assert.Same(forgotten, LegacyCanAttackFlashingRegistration.Forget(forgotten));
        var registration = owned.CanAttackFlashingRegistration;
        var first = owned with { CanAttackFlashingRegistration = registration, Character = null! };
        var last = owned with { Character = null!, CanAttackFlashingRegistration = registration };
        Assert.Null(first.CanAttackFlashingRegistration);
        Assert.Null(last.CanAttackFlashingRegistration);
    }

    [Fact]
    public void CanAttackFlashingTransientRecordSameOwnerActiveCopyCarriesOnlyRuntimeRegistration()
    {
        var owned = Flash0UnitOwned();
        var copy = owned with { Character = owned.Character with
            { Attributes = new Dictionary<string, string>(owned.Character.Attributes) } };
        Assert.Same(owned.CanAttackFlashingRegistration, copy.CanAttackFlashingRegistration);
        Assert.True(LegacyCanAttackFlashingRegistration.TryRead(copy, out _));
        Assert.DoesNotContain(copy.Character.Attributes.Keys,
            key => key.StartsWith("LegacyCanAttackFlashing", StringComparison.Ordinal));
        Assert.False(JsonSerializer.SerializeToElement(copy).TryGetProperty("CanAttackFlashingRegistration", out _));
    }

    private static WorldSession Flash0UnitOwned()
    {
        var session = CreateSession(attributes: new Dictionary<string, string> { ["StatusFlags"] = "1/" });
        return LegacyCanAttackFlashingRegistration.Create(session.Character.Uid,
            new ManualRuntimeTimeProvider(ArrowLifeOrigin)).Install(session);
    }

    private static CharacterSnapshot Flash0UnitCharacter(CharacterSnapshot character, bool active)
    {
        var attributes = new Dictionary<string, string>(character.Attributes) { ["StatusFlags"] = active ? "1/" : "0/" };
        attributes.Remove("BitVector");
        return character with { Attributes = attributes };
    }
}
