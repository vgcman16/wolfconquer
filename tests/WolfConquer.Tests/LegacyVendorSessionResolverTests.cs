using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyVendorSessionResolverTests
{
    private const long BoothUid = 1_393_600;
    private const long MapId = 1_002;

    [Fact]
    public void TryResolveSellerReturnsTheOnlyCurrentSellerAndBoothLocation()
    {
        var connection = CreateConnection();
        var seller = CreateSellerSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, seller);

        var resolved = LegacyVendorSessionResolver.TryResolveSeller(
            sessions,
            BoothUid,
            MapId,
            out var result);

        Assert.True(resolved);
        Assert.Same(connection, result.ActiveSession.Connection);
        Assert.Same(seller, result.ActiveSession.Session);
        Assert.Equal(new LegacyVendorBoothLocation((int)MapId, 411, 354), result.BoothLocation);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2_147_483_648)]
    public void TryResolveSellerRejectsInvalidRequestedBoothUid(long boothUid)
    {
        var sessions = RegisterSeller(out _);

        var resolved = LegacyVendorSessionResolver.TryResolveSeller(
            sessions,
            boothUid,
            MapId,
            out var result);

        Assert.False(resolved);
        Assert.Null(result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2_147_483_648)]
    public void TryResolveSellerRejectsInvalidRequestedMapId(long mapId)
    {
        var sessions = RegisterSeller(out _);

        var resolved = LegacyVendorSessionResolver.TryResolveSeller(
            sessions,
            BoothUid,
            mapId,
            out var result);

        Assert.False(resolved);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("RuntimeVendorUid", "invalid")]
    [InlineData("RuntimeVendorUid", "0")]
    [InlineData("RuntimeVendorUid", "2147483648")]
    [InlineData("RuntimeVendorMapId", "invalid")]
    [InlineData("RuntimeVendorMapId", "0")]
    [InlineData("RuntimeVendorMapId", "2147483648")]
    [InlineData("RuntimeVendorX", "invalid")]
    [InlineData("RuntimeVendorX", "-1")]
    [InlineData("RuntimeVendorX", "65536")]
    [InlineData("RuntimeVendorY", "invalid")]
    [InlineData("RuntimeVendorY", "-1")]
    [InlineData("RuntimeVendorY", "65536")]
    public void TryResolveSellerRejectsMalformedOrOutOfRangeVendorState(string key, string value)
    {
        var attributes = CreateVendorAttributes();
        attributes[key] = value;
        var sessions = RegisterSeller(out _, attributes: attributes);

        var resolved = LegacyVendorSessionResolver.TryResolveSeller(
            sessions,
            BoothUid,
            MapId,
            out var result);

        Assert.False(resolved);
        Assert.Null(result);
    }

    [Fact]
    public void TryResolveSellerRejectsStaleSnapshotSession()
    {
        var connection = CreateConnection();
        var seller = CreateSellerSession();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, seller);
        var staleSnapshot = sessions.Snapshot();
        sessions.Set(connection, seller with { Username = "replacement" });

        var resolved = LegacyVendorSessionResolver.TryResolveSeller(
            sessions,
            staleSnapshot,
            BoothUid,
            MapId,
            out var result);

        Assert.False(resolved);
        Assert.Null(result);
    }

    [Fact]
    public void TryResolveSellerRejectsSellerAndBoothMapMismatch()
    {
        var sessions = RegisterSeller(out _, sellerMapId: MapId + 1);

        var resolved = LegacyVendorSessionResolver.TryResolveSeller(
            sessions,
            BoothUid,
            MapId,
            out var result);

        Assert.False(resolved);
        Assert.Null(result);
    }

    [Fact]
    public void TryResolveSellerRejectsRequestedAndBoothMapMismatch()
    {
        var sessions = RegisterSeller(out _);

        var resolved = LegacyVendorSessionResolver.TryResolveSeller(
            sessions,
            BoothUid,
            MapId + 1,
            out var result);

        Assert.False(resolved);
        Assert.Null(result);
    }

    [Fact]
    public void TryResolveSellerRejectsStaleBoothCoordinates()
    {
        var sessions = RegisterSeller(out _, sellerX: 411);

        var resolved = LegacyVendorSessionResolver.TryResolveSeller(
            sessions,
            BoothUid,
            MapId,
            out var result);

        Assert.False(resolved);
        Assert.Null(result);
    }

    [Fact]
    public void TryResolveSellerRejectsDuplicateBoothUidAmbiguity()
    {
        var sessions = new WorldSessionStore();
        var firstConnection = CreateConnection();
        var secondConnection = CreateConnection();
        sessions.Set(firstConnection, CreateSellerSession(ownerUid: 1_293_600));
        sessions.Set(secondConnection, CreateSellerSession(ownerUid: 1_293_601));

        var resolved = LegacyVendorSessionResolver.TryResolveSeller(
            sessions,
            BoothUid,
            MapId,
            out var result);

        Assert.False(resolved);
        Assert.Null(result);
    }

    private static WorldSessionStore RegisterSeller(
        out ConquerTcpConnection connection,
        long sellerMapId = MapId,
        long sellerX = 410,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        connection = CreateConnection();
        var sessions = new WorldSessionStore();
        sessions.Set(
            connection,
            CreateSellerSession(
                mapId: sellerMapId,
                x: sellerX,
                attributes: attributes));
        return sessions;
    }

    private static WorldSession CreateSellerSession(
        long ownerUid = 1_293_600,
        long mapId = MapId,
        long x = 410,
        long y = 354,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return new WorldSession(
            AccountId: ownerUid,
            Username: $"seller{ownerUid}",
            CreateCharacter(
                ownerUid,
                mapId,
                x,
                y,
                attributes ?? CreateVendorAttributes()),
            PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty);
    }

    private static CharacterSnapshot CreateCharacter(
        long uid,
        long mapId,
        long x,
        long y,
        IReadOnlyDictionary<string, string> attributes)
    {
        return new CharacterSnapshot(
            Uid: uid,
            Name: $"Seller{uid}",
            Body: 1_003,
            Face: 0,
            Hair: 0,
            Class: 142,
            FirstClass: 142,
            SecondClass: 0,
            Reborn: 0,
            Level: 120,
            Experience: 0,
            MapId: mapId,
            X: x,
            Y: y,
            PreviousMapId: mapId,
            PreviousMapX: x,
            PreviousMapY: y,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 300,
            AttributePoints: 0,
            MinHitPoints: 5_000,
            MinMana: 500,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 1,
            SourceFile: "fixture://character",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>());
    }

    private static Dictionary<string, string> CreateVendorAttributes()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RuntimeVendorUid"] = BoothUid.ToString(CultureInfo.InvariantCulture),
            ["RuntimeVendorMapId"] = MapId.ToString(CultureInfo.InvariantCulture),
            ["RuntimeVendorX"] = "411",
            ["RuntimeVendorY"] = "354",
        };
    }

    private static ConquerTcpConnection CreateConnection()
    {
        return new ConquerTcpConnection(null, null);
    }
}
