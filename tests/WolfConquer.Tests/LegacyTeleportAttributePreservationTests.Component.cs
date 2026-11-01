using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyTeleportAttributePreservationTests
{
    [Fact]
    public async Task ActualDestinationExecutorRetainsLowercaseScopeAndTalkReachesOnlyItsRealScope()
    {
        using var f = new Component(Fields(0));
        var sameScope = f.AddPeer(OwnedEquipmentBehaviorFixture.Actor + 10, 88);
        var otherScope = f.AddPeer(OwnedEquipmentBehaviorFixture.Actor + 11, 0);
        var result = Assert.IsType<WorldSession>(await f.TeleportAsync(new(1000, 500, 650, 88)));
        Assert.Same(result, f.Current);
        AssertScopedAttributes(result.Character, StringComparer.Ordinal, true, "88", "44");
        var teleports = f.Sent.Select(Component.ReadFrame).Select(frame =>
            MsgActionPacket.TryParse(frame, out var packet) ? packet : null)
            .Where(packet => packet?.Type == MsgActionType.Teleport).ToArray();
        var teleport = Assert.Single(teleports)!;
        Assert.Equal((1000U, 500U, 650U, 4_321U),
            (teleport.DwParam3, teleport.PositionX, teleport.PositionY, teleport.Timestamp));
        Assert.Contains(sameScope, bytes => LegacyPlayerSpawnPacket.TryParse(Component.ReadFrame(bytes), out var spawn) &&
            spawn.GetNumericField(2) == OwnedEquipmentBehaviorFixture.Actor);
        Assert.DoesNotContain(otherScope, bytes => LegacyPlayerSpawnPacket.TryParse(Component.ReadFrame(bytes), out var spawn) &&
            spawn.GetNumericField(2) == OwnedEquipmentBehaviorFixture.Actor);
        Assert.Contains(f.Sent, bytes => LegacyPlayerSpawnPacket.TryParse(Component.ReadFrame(bytes), out var spawn) &&
            spawn.GetNumericField(2) == OwnedEquipmentBehaviorFixture.Actor + 10);
        Assert.DoesNotContain(f.Sent, bytes => LegacyPlayerSpawnPacket.TryParse(Component.ReadFrame(bytes), out var spawn) &&
            spawn.GetNumericField(2) == OwnedEquipmentBehaviorFixture.Actor + 11);
        f.ClearOutput();
        await f.TalkAsync("scope-preservation-probe");
        Assert.Empty(f.Sent); Assert.Empty(otherScope);
        var talkBytes = Assert.Single(sameScope);
        Assert.True(LegacyMsgMessagePacket.TryParse(Component.ReadFrame(talkBytes), out var talk));
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, talk.ChatType);
        Assert.Equal("scope-preservation-probe", talk.Content[3]);
        AssertScopedAttributes(f.Current.Character, StringComparer.Ordinal, true, "88", "44");
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActualExecutorRefusesEqualOwnedAliasesBeforeAnyPublicationOrOutput(bool secondPrisonTransition)
    {
        var fields = Fields(0);
        if (secondPrisonTransition) { fields["dynamicid"] = "42"; fields.Add("PDinamycID", "777"); }
        else fields.Add("DynamicID", "44");
        using var f = new Component(fields, secondPrisonTransition ? 6000 : 1015);
        var original = f.Current; var before = fields.ToArray(); var publications = f.Publications;
        // Without a terrain catalog the real component route marks80/90 invalid;
        // the first6000/dynamic42 move is coordinate-only, then correction uses
        // the ambiguous previous-scope field. Complete reduction precedes CAS.
        var destination = secondPrisonTransition ? new LegacyTeleportDestination(6000, 80, 90, 42) : new(1000, 500, 650, 88);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => f.TeleportAsync(destination).AsTask());
        Assert.Equal("characterAttributeOwnedFieldAmbiguous", error.Message);
        Assert.Same(original, f.Current); Assert.Equal(publications, f.Publications);
        Assert.Equal(before, fields.ToArray()); Assert.Empty(f.Sent);
    }

    [Fact]
    public async Task ActualTalkRefusesAmbiguousSenderScopeBeforeFirstRecipientOutput()
    {
        var fields = Fields(0); fields.Add("DynamicID", "44");
        using var f = new Component(fields, map: 1000);
        var first = f.AddPeer(OwnedEquipmentBehaviorFixture.Actor + 10, 44);
        var other = f.AddPeer(OwnedEquipmentBehaviorFixture.Actor + 11, 0);
        var before = f.Current; var publications = f.Publications;
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => f.TalkAsync("must-not-be-delivered"));
        Assert.Equal("characterAttributeOwnedFieldAmbiguous", error.Message);
        Assert.Same(before, f.Current); Assert.Equal(publications, f.Publications);
        Assert.Empty(f.Sent); Assert.Empty(first); Assert.Empty(other);
    }
}
