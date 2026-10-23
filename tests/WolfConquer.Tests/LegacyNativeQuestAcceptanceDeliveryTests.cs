using Xunit.Abstractions;

namespace WolfConquer.Tests;

public sealed class LegacyNativeQuestAcceptanceDeliveryTests
{
    private readonly ITestOutputHelper output;

    public LegacyNativeQuestAcceptanceDeliveryTests(ITestOutputHelper output) => this.output = output;

    [Theory]
    [InlineData("QuestAcceptanceOrdinaryDeliveryDiagnosticFault")]
    [InlineData("QuestAcceptanceKingdomDeliveryDiagnosticFault")]
    public void CommittedAcceptanceReturnsTrueWhenRealNativeSendDiagnosticsCannotInitialize(string scenario)
    {
        var result = LegacyNativeConsoleChildFixture.Run(scenario, output).GetProperty("Data");
        Assert.True(result.GetProperty("DiagnosticFaultVerified").GetBoolean());
        Assert.True(result.GetProperty("HandlesRestored").GetBoolean());
        Assert.True(result.GetProperty("NoExceptionFiles").GetBoolean());
        Assert.True(result.GetProperty("SocketMissing").GetBoolean());
        Assert.True(result.GetProperty("SourceCommitted").GetBoolean());
        Assert.True(result.GetProperty("AcceptedIndexCommitted").GetBoolean());
        Assert.True(result.GetProperty("CanonicalOut").GetBoolean());
        Assert.True(result.GetProperty("OriginalMapsUnchanged").GetBoolean());
        Assert.True(result.GetProperty("ExactRow").GetBoolean());
        Assert.True(result.GetProperty("MetadataUnchanged").GetBoolean());
        // These are the red-to-green assertions. The unguarded preimage has
        // already committed both maps/out but escapes TypeInitializationException.
        Assert.Equal(string.Empty, result.GetProperty("AcceptException").GetString());
        Assert.True(result.GetProperty("Returned").GetBoolean());
    }
}
