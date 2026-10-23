using WolfConquer.Updates.Install;

namespace WolfConquer.Updates.Tests;

public sealed class ConfiguredProcessImageReaderWindowsTests
{
    [WindowsFact]
    public void RealReadOnlyHandleResolvesCurrentWindowsProcessWithoutModuleEnumeration()
    {
        string expected = Environment.ProcessPath ?? throw new InvalidOperationException("Test host image unavailable.");
        Assert.Equal(Path.GetFullPath(expected), ConfiguredProcessImageReader.Read(Environment.ProcessId),
            StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidProcessIdsCannotReachNativeOpenOrAuthorizeAnUpdate(int processId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ConfiguredProcessImageReader.Read(processId));
    }
}
