using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class SqlOrdinaryAdmissionDataTests
{
    [Theory]
    [InlineData("missing-plan")]
    [InlineData("foreign-beast")]
    [InlineData("forged-beast-owner")]
    [InlineData("foreign-hairface")]
    [InlineData("missing-beast-metadata")]
    [InlineData("missing-hairface-metadata")]
    [InlineData("malformed-hairface")]
    [InlineData("online-overflow")]
    public Task CompleteNativeRefusalRetainsRealOwnersAndEverySealedSourceImage(string kind) => RunNativeAsync(async (fixture, handler, log) =>
    {
        var character = kind == "malformed-hairface" ? Encoding.UTF8.GetBytes(CharacterText().Replace("1/4201/0/0/", "1/4201/0/0/hidden", StringComparison.Ordinal)) : null;
        var plan = Plan(CompleteBundle(character)); var raw = RawImages(plan.After);
        var wire = await fixture.ConnectGameAsync(false); var birth = await fixture.NextAsync();
        var foreignWire = await fixture.ConnectGameAsync(false); var foreignBirth = await fixture.NextAsync();
        var client = birth.Connection.NativeGameClient!; var foreign = foreignBirth.Connection.NativeGameClient!;
        if (kind is "foreign-beast" or "forged-beast-owner")
        {
            client.Beasts = new(foreign) { Points = 17, flag = 19, FixedLevel = 3 };
            if (kind == "forged-beast-owner") client.Beasts.Owner = client;
        }
        if (kind == "foreign-hairface") client.HairfaceStorage = new(foreign);
        if (kind == "missing-beast-metadata") LegacyNativeBeastsAtrribute.Attributes.Clear();
        if (kind == "missing-hairface-metadata") LegacyNativeHairfaceStorageTable.Hairfaces.Clear();
        if (kind == "online-overflow") client.Player.Online = uint.MaxValue;
        var before = LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(client.Player);
        var foreignBefore = LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(foreign.Player);
        var beasts = client.Beasts; var hairface = client.HairfaceStorage;
        Assert.False(LegacyNativeCapturedCharacterPrefix.TryPrepareFromSqlPlan(birth.Connection,
            kind == "missing-plan" ? null : plan, out var prepared, out var reason));
        Assert.Null(prepared);
        Assert.Equal(kind switch
        {
            "missing-plan" => "capturedNativePrefixPlanMissing",
            "missing-beast-metadata" => "capturedNativePrefixBeastsUnavailable",
            "missing-hairface-metadata" or "malformed-hairface" => "capturedNativePrefixHairfaceUnavailable",
            "online-overflow" => "capturedNativePrefixOnlineOverflow",
            _ => "capturedNativePrefixInvalidData"
        }, reason);
        Assert.Equal(before, LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(client.Player));
        Assert.Equal(foreignBefore, LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(foreign.Player));
        Assert.Same(beasts, client.Beasts); Assert.Same(hairface, client.HairfaceStorage);
        if (beasts is not null) { Assert.Equal(17U, beasts.Points); Assert.Equal(19U, beasts.flag); Assert.Equal((byte)3, beasts.FixedLevel); }
        if (hairface is not null) Assert.Empty(hairface.Objects);
        Assert.Null(foreign.Beasts); Assert.Null(foreign.HairfaceStorage); Assert.Null(client.OrdinarySpellBinding);
        AssertRawUnchanged(raw, plan.After); Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
        Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(foreignBirth.Connection));
        Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text); GC.KeepAlive(wire); GC.KeepAlive(foreignWire);
    });

    [Theory]
    [InlineData("player")]
    [InlineData("beast")]
    [InlineData("hairface-slot")]
    [InlineData("beast-catalog")]
    [InlineData("hairface-catalog")]
    public Task CompletePreparationReadinessCannotSurviveRealNativeOrCatalogDrift(string kind) => RunNativeAsync(async (fixture, handler, _) =>
    {
        var plan = Plan(CompleteBundle()); var raw = RawImages(plan.After);
        var wire = await fixture.ConnectGameAsync(false); var birth = await fixture.NextAsync(); var client = birth.Connection.NativeGameClient!;
        var beasts = new LegacyNativeBeastsRuntime(client) { Points = 17, flag = 19, FixedLevel = 3 };
        var hairface = new LegacyNativeHairfaceStorageRuntime(client); client.Beasts = beasts; client.HairfaceStorage = hairface;
        Assert.True(LegacyNativeCapturedCharacterPrefix.TryPrepareFromSqlPlan(birth.Connection, plan, out var prepared, out var reason), reason);
        var ready = Assert.IsType<LegacyNativeCapturedCharacterPrefix>(prepared); Assert.True(ready.IsCurrent());
        switch (kind)
        {
            case "player": client.Player.Online = 42; break;
            case "beast": beasts.Points = 42; break;
            case "hairface-slot": client.HairfaceStorage = new(client); break;
            case "beast-catalog": LegacyNativeBeastsAtrribute.Attributes[1].RequiredPoints++; break;
            case "hairface-catalog": Assert.Single(LegacyNativeHairfaceStorageTable.Hairfaces).Cost++; break;
        }
        var changedPlayer = LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(client.Player); var changedHairface = client.HairfaceStorage;
        Assert.False(ready.IsCurrent()); Assert.Equal(changedPlayer, LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(client.Player));
        Assert.Same(beasts, client.Beasts); Assert.Same(changedHairface, client.HairfaceStorage);
        Assert.Equal(kind == "beast" ? 42U : 17U, beasts.Points); Assert.Equal(19U, beasts.flag); Assert.Equal((byte)3, beasts.FixedLevel);
        Assert.Empty(hairface.Objects); AssertRawUnchanged(raw, plan.After);
        Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection)); GC.KeepAlive(wire);
    });
}
