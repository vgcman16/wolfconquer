using System.Reflection;
using WolfConquer.Core.Networking;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeLoginControllerTests
{
    [Fact]
    public void CompleteOwnerKeepsOneReplaceablePrivateDictionaryAndThreePublicStaticMethods()
    {
        using var state = new DeclaredState();
        var type = typeof(LegacyNativeLoginController);
        Assert.False(type.IsPublic || type.IsSealed);
        var field = Assert.Single(type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly));
        Assert.Equal("LoginControllerContainer", field.Name);
        Assert.Equal(typeof(Dictionary<string, byte>), field.FieldType);
        Assert.True(field.IsPrivate && field.IsStatic);
        Assert.False(field.IsInitOnly || field.IsLiteral);
        Assert.Equal(new[] { "CanLogin", "Disconnect", "LoggedIn" },
            type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal));
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            var parameter = Assert.Single(method.GetParameters());
            Assert.Equal("IP", parameter.Name);
            Assert.Equal(typeof(string), parameter.ParameterType);
            Assert.False(parameter.IsOptional);
            Assert.Equal(method.Name == "CanLogin" ? typeof(bool) : typeof(void), method.ReturnType);
        }
        Assert.Empty(state.Container);
    }

    [Fact]
    public void FirstCanLoginAddsZeroAndRepeatedQueriesKeepTheSameEntryAndDictionary()
    {
        using var state = new DeclaredState();
        const string key = "MODEL-first";
        Assert.True(LegacyNativeLoginController.CanLogin(key));
        Assert.Equal((byte)0, Assert.Single(state.Container).Value);
        Assert.True(LegacyNativeLoginController.CanLogin(key));
        Assert.Equal((byte)0, Assert.Single(state.Container).Value);
        Assert.Same(state.Container, DeclaredState.ReadContainer());
    }

    [Fact]
    public void CountsZeroOneAndTwoAllowButThreeRejectsWithoutConsumingAQuery()
    {
        using var state = new DeclaredState();
        const string key = "MODEL-threshold";
        Assert.True(LegacyNativeLoginController.CanLogin(key));
        LegacyNativeLoginController.LoggedIn(key);
        Assert.Equal((byte)1, state.Container[key]); Assert.True(LegacyNativeLoginController.CanLogin(key));
        LegacyNativeLoginController.LoggedIn(key);
        Assert.Equal((byte)2, state.Container[key]); Assert.True(LegacyNativeLoginController.CanLogin(key));
        LegacyNativeLoginController.LoggedIn(key);
        Assert.Equal((byte)3, state.Container[key]); Assert.False(LegacyNativeLoginController.CanLogin(key));
        Assert.False(LegacyNativeLoginController.CanLogin(key)); Assert.Equal((byte)3, state.Container[key]);
    }

    [Fact]
    public void LoggedInDoesNotCreateAnUnknownEntry()
    {
        using var state = new DeclaredState();
        LegacyNativeLoginController.LoggedIn("MODEL-unknown");
        Assert.Empty(state.Container);
        Assert.Same(state.Container, DeclaredState.ReadContainer());
    }

    [Fact]
    public void LoggedInContinuesAboveTheAdmissionThresholdRatherThanCappingAtThree()
    {
        using var state = new DeclaredState();
        const string key = "MODEL-no-cap";
        Assert.True(LegacyNativeLoginController.CanLogin(key));
        for (var index = 0; index < 5; index++) LegacyNativeLoginController.LoggedIn(key);
        Assert.Equal((byte)5, state.Container[key]);
        Assert.False(LegacyNativeLoginController.CanLogin(key));
        Assert.Single(state.Container);
    }

    [Fact]
    public void DisconnectDoesNotCreateUnknownKeysOrRemoveAnExistingZeroKey()
    {
        using var state = new DeclaredState();
        LegacyNativeLoginController.Disconnect("MODEL-unknown");
        Assert.Empty(state.Container);
        Assert.True(LegacyNativeLoginController.CanLogin("MODEL-zero"));
        LegacyNativeLoginController.Disconnect("MODEL-zero");
        LegacyNativeLoginController.Disconnect("MODEL-zero");
        Assert.Equal((byte)0, state.Container["MODEL-zero"]);
        Assert.Single(state.Container);
        Assert.True(LegacyNativeLoginController.CanLogin("MODEL-zero"));
    }

    [Fact]
    public void DisconnectReopensOnlyAfterTheRetainedCountDropsBelowThree()
    {
        using var state = new DeclaredState();
        const string key = "MODEL-reopen";
        Assert.True(LegacyNativeLoginController.CanLogin(key));
        for (var index = 0; index < 4; index++) LegacyNativeLoginController.LoggedIn(key);
        LegacyNativeLoginController.Disconnect(key);
        Assert.Equal((byte)3, state.Container[key]); Assert.False(LegacyNativeLoginController.CanLogin(key));
        LegacyNativeLoginController.Disconnect(key);
        Assert.Equal((byte)2, state.Container[key]); Assert.True(LegacyNativeLoginController.CanLogin(key));
        Assert.Same(state.Container, DeclaredState.ReadContainer());
    }

    [Fact]
    public void TheTwoHundredFiftySixthRecordedLoginWrapsTheNativeByteBackToZero()
    {
        using var state = new DeclaredState();
        const string key = "MODEL-byte-wrap";
        Assert.True(LegacyNativeLoginController.CanLogin(key));
        for (var index = 0; index < 255; index++) LegacyNativeLoginController.LoggedIn(key);
        Assert.Equal(byte.MaxValue, state.Container[key]);
        Assert.False(LegacyNativeLoginController.CanLogin(key));
        checked { LegacyNativeLoginController.LoggedIn(key); }
        Assert.Equal((byte)0, state.Container[key]);
        Assert.True(LegacyNativeLoginController.CanLogin(key));
        Assert.Single(state.Container);
    }

    [Fact]
    public void NullKeyEscapesCanLoginButIsSwallowedByBothMutatorCatchBodies()
    {
        using var state = new DeclaredState();
        Assert.Throws<ArgumentNullException>(() => LegacyNativeLoginController.CanLogin(null!));
        Assert.Null(Record.Exception(() => LegacyNativeLoginController.LoggedIn(null!)));
        Assert.Null(Record.Exception(() => LegacyNativeLoginController.Disconnect(null!)));
        Assert.Empty(state.Container);
        Assert.Same(state.Container, DeclaredState.ReadContainer());
    }

    [Fact]
    public void DeclaredNullContainerFaultEscapesCanLoginWithoutARecoveryReplacement()
    {
        using var state = new DeclaredState();
        state.ReplaceContainer(null);
        Assert.Throws<NullReferenceException>(() => LegacyNativeLoginController.CanLogin("MODEL-null"));
        Assert.Null(Record.Exception(() => LegacyNativeLoginController.LoggedIn("MODEL-null")));
        Assert.Null(Record.Exception(() => LegacyNativeLoginController.Disconnect("MODEL-null")));
        Assert.Null(DeclaredState.ReadContainer());
        Assert.Empty(state.Container);
    }

    [Fact]
    public void DefaultDictionaryTreatsEmptyAndDifferentlyCasedKeysAsIndependentStrings()
    {
        using var state = new DeclaredState();
        foreach (var key in new[] { "", "MODEL-endpoint", "model-endpoint" })
            Assert.True(LegacyNativeLoginController.CanLogin(key));
        LegacyNativeLoginController.LoggedIn("MODEL-endpoint");
        Assert.Equal((byte)0, state.Container[""]);
        Assert.Equal((byte)1, state.Container["MODEL-endpoint"]);
        Assert.Equal((byte)0, state.Container["model-endpoint"]);
        Assert.Equal(3, state.Container.Count);
    }

    [Fact]
    public void IndependentOwnerInstancesShareTheOneNativeStaticDictionary()
    {
        using var state = new DeclaredState();
        var first = new LegacyNativeLoginController();
        var second = new LegacyNativeLoginController();
        Assert.NotSame(first, second);
        Assert.True(LegacyNativeLoginController.CanLogin("MODEL-shared"));
        LegacyNativeLoginController.LoggedIn("MODEL-shared");
        Assert.Equal((byte)1, state.Container["MODEL-shared"]);
        Assert.Same(state.Container, DeclaredState.ReadContainer());
        Assert.Empty(typeof(LegacyNativeLoginController).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly));
    }

    // Reflection substitutes MODEL state only; no production reset or host adoption is added.
    // Every declared fixture restores the exact original reference through using/finally.
    private sealed class DeclaredState : IDisposable
    {
        private static readonly FieldInfo Field = typeof(LegacyNativeLoginController)
            .GetField("LoginControllerContainer", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? original;
        public Dictionary<string, byte> Container { get; } = new();

        public DeclaredState()
        {
            original = Field.GetValue(null);
            Field.SetValue(null, Container);
        }

        public void ReplaceContainer(Dictionary<string, byte>? value) => Field.SetValue(null, value);
        public static object? ReadContainer() => Field.GetValue(null);
        public void Dispose() => Field.SetValue(null, original);
    }
}
