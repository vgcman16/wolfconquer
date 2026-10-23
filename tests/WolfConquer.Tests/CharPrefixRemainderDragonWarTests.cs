using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class CharPrefixRemainderDragonWarTests
{
    [Theory]
    [InlineData("StatusFlags", "0/0/0/0/0/9/", "0/0/0/0/0/8/")]
    [InlineData("StatusFlags", "0/0/0/0/0/9", "0/0/0/0/0/8")]
    [InlineData("StatusFlags", "0,0,4294967304", "0,0,8")]
    [InlineData("BitVector", "0/0/0/0/0/9/", "0/0/0/0/0/8/")]
    [InlineData("StatusFlags", "0/0/0/0/0/8/", "0/0/0/0/0/8/")]
    public void JoinRemovesDragonSwingAndPreservesStatusFormatAndUnrelatedBit(
        string key, string before, string expected)
    {
        var character = CharacterSnapshotParser.ParseText("""
            [Character]
            UID=1293600
            Name=PrefixFixture
            Body=1003
            Face=0
            Haire=0
            Class=1005
            FirstClass=0
            SecoundeClass=0
            Reborn=0
            Level=50
            Experience=0
            Map=1002
            X=500
            Y=650
            PMap=1002
            PMapX=500
            PMapY=650
            Strength=0
            Agility=0
            Vitaliti=0
            Spirit=0
            Atributes=0
            MinHitPoints=100
            MinMana=10
            Money=0
            ConquerPoints=0
            BoundConquerPoints=0
            BankCps=0
            PkPoints=0
            Online=1
            """);
        var attributes = new Dictionary<string, string>(character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            [key] = before,
            ["UnrelatedSentinel"] = "preserved",
        };
        var session = new WorldSession(character.Uid, "synthetic-prefix-owner",
            character with { Attributes = attributes }, PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty);
        var store = new LegacyDragonWarRuntimeStateStore();
        store.Open(new DateTimeOffset(2026, 10, 23, 3, 0, 0, TimeSpan.Zero));

        Assert.True(store.TryJoin(session, out var joined));

        Assert.Equal(expected, joined.Character.Attributes[key]);
        Assert.Equal("preserved", joined.Character.Attributes["UnrelatedSentinel"]);
        Assert.Equal(before, session.Character.Attributes[key]);
        Assert.NotSame(session, joined);
        Assert.Equal(LegacyDragonWarRuntimeStateStore.DragonWarMapId, joined.Character.MapId);
    }
}
