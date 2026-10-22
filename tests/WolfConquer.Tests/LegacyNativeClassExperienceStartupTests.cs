using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeClassExperienceStartupTests
{
    [Theory]
    [InlineData("-1", 0u)]
    [InlineData("4294967296", 0u)]
    [InlineData("00000000000035ignored", 35u)]
    [InlineData("not-uint", 0u)]
    public void ExplicitPhysicalPlayerSuppliesLoadedFieldsWithoutNormalizingSnapshot(string raw, uint nativeValue)
    {
        var snapshot = Snapshot(raw);
        var attributes = snapshot.Attributes;
        var before = attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
        var owner = new LegacyNativeGameClientRuntime();
        var player = owner.Player;
        var flags = player.BitVector;
        var random = player.MyRandom;
        player.UID = 7654321;
        player.Online = 9;
        player.DeityLandLuckyPoints = 11;
        player.HeroPoints = 22;
        player.Body = 2002;
        player.Face = 17;
        player.TransformationID = 65535;
        player.Name = snapshot.Name;
        player.Spouse = "None";
        player.ClassExperience = nativeValue; // Controlled already-loaded physical field, no INI/host claim.
        Assert.False(owner.FullLoading);
        var baselineHero = LegacyProtoHeroInfoPacket.FromSnapshot(Snapshot("5500"), serverId: 7, initTransfer: 3);

        var hero = LegacyProtoHeroInfoPacket.FromSnapshot(snapshot, serverId: 7, initTransfer: 3, nativePlayer: player);
        var update = LegacyMsgUpdatePacket.CreateLegacyClassExperienceStartupUpdate(snapshot, nativePlayer: player);
        var loader = LegacyLoaderHeroInfoPacket.FromSnapshot(snapshot, serverId: 7, nativePlayer: player);

        Assert.Equal((ulong)nativeValue, hero.GetNumericField(43));
        const uint physicalMesh = unchecked((uint)(65535 * 10_000_000 + 17 * 10_000 + 2002));
        Assert.Equal(physicalMesh, player.Mesh);
        Assert.Equal((ulong)physicalMesh, hero.GetNumericField(3));
        Assert.Equal(physicalMesh, loader.Mesh);
        Assert.Equal(snapshot.Name, loader.Name);
        Assert.Equal(1293600u, loader.Uid);
        Assert.Equal((ushort)7, loader.ServerId);
        Assert.Equal(7051u, loader.Class);
        Assert.Equal((byte)140, loader.Level);
        Assert.True(LegacyLoaderHeroInfoPacket.TryParse(Frame(loader.ToFrameBytes()), out var parsedLoader));
        Assert.Equal(loader, parsedLoader);
        Assert.NotEqual(baselineHero.GetNumericField(3), hero.GetNumericField(3));
        Assert.Equal(1293600ul, hero.GetNumericField(1)); // Physical UID is not a replacement source.
        for (var field = 1; field <= 49; field++)
        {
            if (field is 3 or 43 or 46 or 47) continue;
            Assert.Equal(baselineHero.GetNumericField(field), hero.GetNumericField(field));
        }
        Assert.Equal(baselineHero.GetStringField(46), hero.GetStringField(46));
        Assert.Equal(baselineHero.GetStringField(47), hero.GetStringField(47));
        Assert.Equal(baselineHero.GetStringField(50), hero.GetStringField(50));
        Assert.Equal(1293600u, update.Uid);
        var entry = Assert.Single(update.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.ClassExperience, entry.Type);
        Assert.Equal((ulong)nativeValue, entry.Value);
        Assert.True(LegacyProtoHeroInfoPacket.TryParse(Frame(hero.ToFrameBytes()), out var parsedHero));
        Assert.Equal((ulong)nativeValue, parsedHero.GetNumericField(43));
        Assert.Equal((ulong)physicalMesh, parsedHero.GetNumericField(3));
        Assert.Equal(1293600ul, parsedHero.GetNumericField(1));
        Assert.True(LegacyMsgUpdatePacket.TryParse(Frame(update.ToFrameBytes()), out var parsedUpdate));
        Assert.Equal((ulong)nativeValue, Assert.Single(parsedUpdate.Updates).Value);
        Assert.Equal(1293600u, parsedUpdate.Uid);

        Assert.Same(player, owner.Player);
        Assert.Same(owner, player.Owner);
        Assert.Same(flags, player.BitVector);
        Assert.Same(random, player.MyRandom);
        Assert.Equal(nativeValue, player.ClassExperience);
        Assert.Equal(7654321u, player.UID);
        Assert.Equal(9u, player.Online);
        Assert.Equal(11u, player.DeityLandLuckyPoints);
        Assert.Equal(22u, player.HeroPoints);
        Assert.Equal((ushort)2002, player.Body);
        Assert.Equal((ushort)17, player.Face);
        Assert.Equal((ushort)65535, player.TransformationID);
        Assert.False(owner.FullLoading);
        Assert.False(owner.Fake);
        Assert.Null(owner.Socket);
        Assert.Same(attributes, snapshot.Attributes);
        Assert.Equal(raw, snapshot.Attributes["ClassExperience"]);
        Assert.Equal(before, snapshot.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray());
        Assert.Equal(1293600L, snapshot.Uid);
        Assert.Equal(7L, snapshot.Online);
    }

    [Theory]
    [InlineData("-1", ulong.MaxValue, "negative")]
    [InlineData("4294967296", 4294967296ul, "valid-long")]
    [InlineData("00000000000035ignored", 0ul, "malformed")]
    [InlineData("not-uint", 0ul, "malformed")]
    public void NullOrOmittedPhysicalPlayerRetainsExistingSnapshotOnlyFactorySemantics(
        string raw, ulong expectedHero, string updateBehavior)
    {
        var snapshot = Snapshot(raw);
        var attributes = snapshot.Attributes;
        var before = attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();

        var omitted = LegacyProtoHeroInfoPacket.FromSnapshot(snapshot);
        var explicitNull = LegacyProtoHeroInfoPacket.FromSnapshot(snapshot, nativePlayer: null);

        Assert.Equal(expectedHero, omitted.GetNumericField(43));
        Assert.Equal(expectedHero, explicitNull.GetNumericField(43));
        Assert.Equal(omitted.ToFrameBytes(), explicitNull.ToFrameBytes());
        switch (updateBehavior)
        {
            case "negative":
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    LegacyMsgUpdatePacket.CreateLegacyClassExperienceStartupUpdate(snapshot));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    LegacyMsgUpdatePacket.CreateLegacyClassExperienceStartupUpdate(snapshot, nativePlayer: null));
                break;
            case "malformed":
                Assert.Throws<FormatException>(() =>
                    LegacyMsgUpdatePacket.CreateLegacyClassExperienceStartupUpdate(snapshot));
                Assert.Throws<FormatException>(() =>
                    LegacyMsgUpdatePacket.CreateLegacyClassExperienceStartupUpdate(snapshot, nativePlayer: null));
                break;
            case "valid-long":
                var update = LegacyMsgUpdatePacket.CreateLegacyClassExperienceStartupUpdate(snapshot);
                var nullUpdate = LegacyMsgUpdatePacket.CreateLegacyClassExperienceStartupUpdate(snapshot, nativePlayer: null);
                Assert.Equal(expectedHero, Assert.Single(update.Updates).Value);
                Assert.Equal(update.ToFrameBytes(), nullUpdate.ToFrameBytes());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(updateBehavior));
        }
        Assert.Same(attributes, snapshot.Attributes);
        Assert.Equal(raw, snapshot.Attributes["ClassExperience"]);
        Assert.Equal(before, snapshot.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray());
    }

    private static PacketFrame Frame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static CharacterSnapshot Snapshot(string rawClassExperience) => CharacterSnapshotParser.ParseText($$"""
        [Character]
        UID=1293600
        Name=ClassXpFixture
        Body=1008
        Face=154
        Haire=27
        Class=7051
        FirstClass=0
        SecoundeClass=7050
        Reborn=2
        Level=140
        Experience=0
        Map=1015
        X=830
        Y=584
        PMap=1000
        PMapX=500
        PMapY=650
        Strength=0
        Agility=36
        Vitaliti=447
        Spirit=0
        Atributes=12
        MinHitPoints=61258
        MinMana=5000
        Money=123
        ConquerPoints=0
        BoundConquerPoints=0
        BankCps=0
        PkPoints=7
        Online=7
        ClassExperience={{rawClassExperience}}
        Spouse=None
        Signature=Keep signature
        Keep=exact-raw
        """);
}
