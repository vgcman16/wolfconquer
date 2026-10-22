using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Pure physical-owner provenance, not Data132 send ordering or actual-client proof.
public sealed class LegacyCompleteLoginRuntimeTests
{
    private const uint Uid = 1_300_101;

    [Fact]
    public void PhysicalBirthCaptureSetAndCasDoNotCompleteLogin()
    {
        var (connection, session) = Create();
        var player = connection.NativeGameClient!.Player;
        Assert.False(player.CompleteLogin);
        Assert.True(LegacyCompleteLoginRuntime.TryCapture(connection, session, out var owner));
        Assert.NotNull(owner);
        Assert.False(player.CompleteLogin);
        Assert.False(LegacyCompleteLoginRuntime.IsComplete(connection, session));
        var store = new WorldSessionStore();
        store.Set(connection, session);
        var copied = session with { Character = session.Character with { Money = 500 } };
        Assert.True(store.TryCompareAndSet(connection, session, copied));
        Assert.False(player.CompleteLogin);
        Assert.False(LegacyCompleteLoginRuntime.IsComplete(connection, copied));
    }

    [Fact]
    public void RealCompletionRetainsThePhysicalOwnerAcrossLawfulImmutableCopiesAndRepeatedCompletion()
    {
        var (connection, session) = Create();
        Assert.True(LegacyCompleteLoginRuntime.TryCapture(connection, session, out var captured));
        var owner = Assert.IsType<LegacyCompleteLoginRuntime.Owner>(captured);
        var attributes = new Dictionary<string, string>(session.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        { ["CompleteLogin"] = "0", ["RuntimeCosmetic"] = "changed" };
        var current = session with { Character = session.Character with { Money = 999, Attributes = attributes } };
        Assert.True(LegacyCompleteLoginRuntime.Matches(owner, connection, current));
        Assert.True(LegacyCompleteLoginRuntime.TryComplete(owner, connection, current));
        Assert.True(owner.Player.CompleteLogin);
        Assert.True(LegacyCompleteLoginRuntime.IsComplete(connection, session));
        Assert.True(LegacyCompleteLoginRuntime.IsComplete(connection, current));
        owner.Player.IsCheckedPass = false;
        owner.Player.SecurityPassword = 1234;
        Assert.True(LegacyCompleteLoginRuntime.IsComplete(connection, current));
        Assert.True(LegacyCompleteLoginRuntime.TryComplete(owner, connection, current with { }));
        Assert.True(LegacyCompleteLoginRuntime.IsComplete(connection, current));
        Assert.Same(owner.Player, current.NativePlayer);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("0")]
    public void SavedAttributesForgedPublicFieldsAndPinVerificationDoNotIssueCompletion(string savedValue)
    {
        var (connection, session) = Create();
        var attributes = new Dictionary<string, string>(session.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        { ["CompleteLogin"] = savedValue, ["IsCheckedPass"] = "true" };
        session = session with { Character = session.Character with { Attributes = attributes } };
        var player = connection.NativeGameClient!.Player;
        player.CompleteLogin = true;
        player.IsCheckedPass = true;
        Assert.True(LegacyCompleteLoginRuntime.TryCapture(connection, session, out _));
        Assert.False(LegacyCompleteLoginRuntime.IsComplete(connection, session));
    }

    [Fact]
    public void SameUidPlayerReplacementCannotReuseCapturedOwnerOrIssuedCompletion()
    {
        var (connection, session) = Create();
        Assert.True(LegacyCompleteLoginRuntime.TryCapture(connection, session, out var captured));
        var owner = Assert.IsType<LegacyCompleteLoginRuntime.Owner>(captured);
        Assert.True(LegacyCompleteLoginRuntime.TryComplete(owner, connection, session));
        var replacement = new LegacyNativePlayerRuntime(owner.Client) { UID = Uid };
        Assert.False(replacement.CompleteLogin);
        owner.Client.Player = replacement;
        Assert.False(LegacyCompleteLoginRuntime.IsComplete(connection, session));
        replacement.CompleteLogin = true;
        Assert.False(LegacyCompleteLoginRuntime.Matches(owner, connection, session));
        Assert.False(LegacyCompleteLoginRuntime.TryComplete(owner, connection, session));
        Assert.False(LegacyCompleteLoginRuntime.IsComplete(connection, session));
        Assert.True(owner.Player.CompleteLogin);
        Assert.True(LegacyCompleteLoginRuntime.TryCapture(connection, session, out var fresh));
        Assert.True(LegacyCompleteLoginRuntime.TryComplete(Assert.IsType<LegacyCompleteLoginRuntime.Owner>(fresh), connection, session));
        Assert.True(LegacyCompleteLoginRuntime.IsComplete(connection, session));
    }

    [Theory]
    [InlineData("connection")]
    [InlineData("session-client")]
    [InlineData("account")]
    [InlineData("session-uid")]
    [InlineData("player-uid")]
    [InlineData("player-owner")]
    public void CapturedAndIssuedCompletionCannotMoveToAnotherIdentity(string scenario)
    {
        var (connection, session) = Create();
        Assert.True(LegacyCompleteLoginRuntime.TryCapture(connection, session, out var captured));
        var owner = Assert.IsType<LegacyCompleteLoginRuntime.Owner>(captured);
        Assert.True(LegacyCompleteLoginRuntime.TryComplete(owner, connection, session));
        switch (scenario)
        {
            case "connection": connection = new(null, null, nativeGameClient: owner.Client); break;
            case "session-client": session = session with { NativeGameClient = new LegacyNativeGameClientRuntime() }; break;
            case "account": session = session with { AccountId = session.AccountId + 1 }; break;
            case "session-uid": session = session with { Character = session.Character with { Uid = Uid + 1 } }; break;
            case "player-uid": owner.Player.UID = Uid + 1; break;
            case "player-owner": owner.Client.Player = new LegacyNativeGameClientRuntime().Player; owner.Client.Player.UID = Uid; break;
        }
        Assert.False(LegacyCompleteLoginRuntime.Matches(owner, connection, session));
        Assert.False(LegacyCompleteLoginRuntime.TryComplete(owner, connection, session));
        Assert.False(LegacyCompleteLoginRuntime.IsComplete(connection, session));
    }

    [Theory]
    [InlineData(0L, 1_300_101L)]
    [InlineData(-1L, 1_300_101L)]
    [InlineData(1L, 0L)]
    [InlineData(1L, -1L)]
    [InlineData(1L, 4_294_967_296L)]
    public void InvalidAccountOrUnrepresentableUidCannotCaptureAnOwner(long accountId, long uid)
    {
        var (connection, session) = Create();
        session = session with { AccountId = accountId, Character = session.Character with { Uid = uid } };
        Assert.False(LegacyCompleteLoginRuntime.TryCapture(connection, session, out var owner));
        Assert.Null(owner);
        Assert.False(LegacyCompleteLoginRuntime.IsComplete(connection, session));
    }

    [Fact]
    public void WhollyUnboundSessionsHaveNoCompletionFallback()
    {
        var (_, bound) = Create();
        var connection = new ConquerTcpConnection(null, null);
        var attributes = new Dictionary<string, string>(bound.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        { ["CompleteLogin"] = "1" };
        var session = bound with { NativeGameClient = null, Character = bound.Character with { Attributes = attributes } };
        Assert.False(LegacyCompleteLoginRuntime.TryCapture(connection, session, out var owner));
        Assert.Null(owner);
        Assert.False(LegacyCompleteLoginRuntime.IsComplete(connection, session));
    }

    [Fact]
    public void ObservedFalseFieldInvalidatesIssuanceAndOnlyRealCompletionCanRenewIt()
    {
        var (connection, session) = Create();
        Assert.True(LegacyCompleteLoginRuntime.TryCapture(connection, session, out var captured));
        var owner = Assert.IsType<LegacyCompleteLoginRuntime.Owner>(captured);
        Assert.True(LegacyCompleteLoginRuntime.TryComplete(owner, connection, session));
        owner.Player.CompleteLogin = false;
        Assert.False(LegacyCompleteLoginRuntime.IsComplete(connection, session));
        owner.Player.CompleteLogin = true;
        Assert.False(LegacyCompleteLoginRuntime.IsComplete(connection, session));
        Assert.True(LegacyCompleteLoginRuntime.TryComplete(owner, connection, session));
        Assert.True(LegacyCompleteLoginRuntime.IsComplete(connection, session));
    }

    private static (ConquerTcpConnection Connection, WorldSession Session) Create()
    {
        var client = new LegacyNativeGameClientRuntime();
        client.Player.UID = Uid;
        var connection = new ConquerTcpConnection(null, null, nativeGameClient: client);
        var numeric = new[] { "Body", "Face", "Haire", "Class", "FirstClass", "SecoundeClass", "Reborn", "Level",
            "Experience", "Map", "X", "Y", "PMap", "PMapX", "PMapY", "Strength", "Agility", "Vitaliti", "Spirit",
            "Atributes", "MinHitPoints", "MinMana", "Money", "ConquerPoints", "BoundConquerPoints", "BankCps", "PkPoints", "Online" };
        var text = "[Character]\nUID=" + Uid.ToString(CultureInfo.InvariantCulture) + "\nName=CompletionFixture\n" +
            string.Join('\n', numeric.Select(key => key + "=0")) + "\nSecurityPass=0,0,0\n";
        var session = new WorldSession(Uid, "fixture", CharacterSnapshotParser.ParseText(text), PlayerItemSnapshot.Empty,
            PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty) { NativeGameClient = client };
        return (connection, session);
    }
}
