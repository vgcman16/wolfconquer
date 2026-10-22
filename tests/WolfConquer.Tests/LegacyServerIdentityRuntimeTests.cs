using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Pure native-field/configuration provenance, not actual LoadMap send-order proof.
public sealed class LegacyServerIdentityRuntimeTests
{
    private const uint Uid = 1_300_201;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \t\r\n")]
    public void MissingOrWhitespaceEnvironmentHasNoGuessedIdentity(string? text)
    {
        var reads = 0;
        var identity = LegacyLocalServerIdentity.FromEnvironment(name =>
        {
            Assert.Equal("WOLFCONQUER_LOCAL_SERVER_ID", name);
            reads++;
            return text;
        });
        Assert.Null(identity);
        Assert.Equal(1, reads);
    }

    [Theory]
    [InlineData("0", 0u)]
    [InlineData("42", 42u)]
    [InlineData(" 00042 \t", 42u)]
    [InlineData("65535", 65_535u)]
    [InlineData("65536", 65_536u)]
    [InlineData("4294967295", uint.MaxValue)]
    public void EnvironmentParsesTrimmedStrictUnsignedDecimalWithoutNarrowing(string text, uint expected)
    {
        var identity = LegacyLocalServerIdentity.FromEnvironment(_ => text);
        Assert.Equal(expected, Assert.IsType<LegacyLocalServerIdentity>(identity).ServerId);
    }

    [Theory]
    [InlineData("+42")]
    [InlineData("-1")]
    [InlineData("4294967296")]
    [InlineData("0x2A")]
    [InlineData("4.2")]
    [InlineData("4e2")]
    [InlineData("4 2")]
    [InlineData("not-a-server-id")]
    public void MalformedEnvironmentThrowsGenericConfigurationError(string text)
    {
        var error = Assert.Throws<FormatException>(() => LegacyLocalServerIdentity.FromEnvironment(_ => text));
        Assert.Equal("Local server identity configuration is invalid.", error.Message);
        Assert.DoesNotContain(text, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BirthAndUnconfiguredInitializationLeaveDefaultFieldUnissuedEvenForZeroConfiguration()
    {
        var (connection, session) = Create();
        var player = connection.NativeGameClient!.Player;
        Assert.Equal((ushort)0, player.ServerID);
        Assert.False(LegacyServerIdentityRuntime.TryInitialize(connection, session, null));
        Assert.Equal((ushort)0, player.ServerID);
        Assert.False(LegacyServerIdentityRuntime.IsOnOwnServer(connection, session, null));
        Assert.False(LegacyServerIdentityRuntime.IsOnOwnServer(connection, session, new(0)));
    }

    [Theory]
    [InlineData(0u, (ushort)0, true)]
    [InlineData(42u, (ushort)42, true)]
    [InlineData(65_535u, (ushort)65_535, true)]
    [InlineData(65_536u, (ushort)0, false)]
    [InlineData(uint.MaxValue, (ushort)65_535, false)]
    public void InitializedFieldWrapsAsUshortButOwnServerComparesAgainstFullUint(uint configured, ushort field, bool own)
    {
        var (connection, session) = Create();
        connection.NativeGameClient!.Player.ServerID = 17;
        Assert.True(LegacyServerIdentityRuntime.TryInitialize(connection, session, new(configured)));
        Assert.Equal(field, connection.NativeGameClient!.Player.ServerID);
        Assert.Equal(own, LegacyServerIdentityRuntime.IsOnOwnServer(connection, session, new(configured)));
    }

    [Fact]
    public void LawfulCopiesRetainIdentityWithoutUsingSavedFlagsPinOrLoginCompletion()
    {
        var (connection, session) = Create();
        var identity = new LegacyLocalServerIdentity(42);
        Assert.True(LegacyServerIdentityRuntime.TryInitialize(connection, session, identity));
        var attrs = new Dictionary<string, string>(session.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        { ["ServerID"] = "65535", ["OnMyOwnServer"] = "false", ["CompleteLogin"] = "0" };
        var copied = session with { Character = session.Character with { Money = 500, Attributes = attrs } };
        connection.NativeGameClient!.Player.IsCheckedPass = false;
        connection.NativeGameClient.Player.CompleteLogin = false;
        Assert.True(LegacyServerIdentityRuntime.IsOnOwnServer(connection, copied, identity));
        Assert.True(LegacyServerIdentityRuntime.IsOnOwnServer(connection, copied, identity with { }));
        Assert.False(LegacyServerIdentityRuntime.IsOnOwnServer(connection, copied, new(43)));
    }

    [Fact]
    public void SavedAttributesAndDirectMatchingFieldWithoutIssuanceDoNotGrantIdentity()
    {
        var (connection, session) = Create();
        var attrs = new Dictionary<string, string>(session.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        { ["ServerID"] = "42", ["OnMyOwnServer"] = "true" };
        session = session with { Character = session.Character with { Attributes = attrs } };
        connection.NativeGameClient!.Player.ServerID = 42;
        Assert.False(LegacyServerIdentityRuntime.IsOnOwnServer(connection, session, new(42)));
    }

    [Theory]
    [InlineData("connection")]
    [InlineData("client")]
    [InlineData("account")]
    [InlineData("session-uid")]
    [InlineData("player-uid")]
    [InlineData("player-owner")]
    [InlineData("field")]
    public void IssuedIdentityCannotMoveToForeignOwnersOrForgedField(string scenario)
    {
        var (connection, session) = Create();
        var client = connection.NativeGameClient!;
        Assert.True(LegacyServerIdentityRuntime.TryInitialize(connection, session, new(42)));
        switch (scenario)
        {
            case "connection": connection = new(null, null, nativeGameClient: client); break;
            case "client": session = session with { NativeGameClient = new LegacyNativeGameClientRuntime() }; break;
            case "account": session = session with { AccountId = session.AccountId + 1 }; break;
            case "session-uid": session = session with { Character = session.Character with { Uid = Uid + 1 } }; break;
            case "player-uid": client.Player.UID = Uid + 1; break;
            case "player-owner": client.Player = new LegacyNativeGameClientRuntime().Player; client.Player.UID = Uid; client.Player.ServerID = 42; break;
            case "field": client.Player.ServerID = 43; break;
        }
        Assert.False(LegacyServerIdentityRuntime.IsOnOwnServer(connection, session, new(42)));
    }

    [Fact]
    public void SameUidPlayerReplacementNeedsItsOwnRealInitialization()
    {
        var (connection, session) = Create();
        var client = connection.NativeGameClient!;
        Assert.True(LegacyServerIdentityRuntime.TryInitialize(connection, session, new(42)));
        var replacement = new LegacyNativePlayerRuntime(client) { UID = Uid, ServerID = 42 };
        client.Player = replacement;
        Assert.False(LegacyServerIdentityRuntime.IsOnOwnServer(connection, session, new(42)));
        Assert.True(LegacyServerIdentityRuntime.TryInitialize(connection, session, new(42)));
        Assert.True(LegacyServerIdentityRuntime.IsOnOwnServer(connection, session, new(42)));
    }

    [Theory]
    [InlineData(0L, 1_300_201L)]
    [InlineData(-1L, 1_300_201L)]
    [InlineData(1L, 0L)]
    [InlineData(1L, -1L)]
    [InlineData(1L, 4_294_967_296L)]
    public void InvalidAccountOrUidCannotInitializeOrMutateTheField(long account, long uid)
    {
        var (connection, session) = Create();
        connection.NativeGameClient!.Player.ServerID = 17;
        session = session with { AccountId = account, Character = session.Character with { Uid = uid } };
        Assert.False(LegacyServerIdentityRuntime.TryInitialize(connection, session, new(42)));
        Assert.Equal((ushort)17, connection.NativeGameClient.Player.ServerID);
        Assert.False(LegacyServerIdentityRuntime.IsOnOwnServer(connection, session, new(42)));
    }

    [Fact]
    public void WhollyUnboundManagedSessionsHaveNoSavedIdentityFallback()
    {
        var (_, original) = Create();
        var connection = new ConquerTcpConnection(null, null);
        var session = original with { NativeGameClient = null };
        Assert.False(LegacyServerIdentityRuntime.TryInitialize(connection, session, new(0)));
        Assert.False(LegacyServerIdentityRuntime.IsOnOwnServer(connection, session, new(0)));
    }

    private static (ConquerTcpConnection Connection, WorldSession Session) Create()
    {
        var client = new LegacyNativeGameClientRuntime();
        client.Player.UID = Uid;
        var connection = new ConquerTcpConnection(null, null, nativeGameClient: client);
        var numeric = new[] { "Body", "Face", "Haire", "Class", "FirstClass", "SecoundeClass", "Reborn", "Level",
            "Experience", "Map", "X", "Y", "PMap", "PMapX", "PMapY", "Strength", "Agility", "Vitaliti", "Spirit",
            "Atributes", "MinHitPoints", "MinMana", "Money", "ConquerPoints", "BoundConquerPoints", "BankCps", "PkPoints", "Online" };
        var text = "[Character]\nUID=" + Uid.ToString(CultureInfo.InvariantCulture) + "\nName=ServerIdentityFixture\n" +
            string.Join('\n', numeric.Select(key => key + "=0")) + "\nSecurityPass=0,0,0\n";
        return (connection, new WorldSession(Uid, "fixture", CharacterSnapshotParser.ParseText(text), PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty) { NativeGameClient = client });
    }
}
