using System.Reflection;

namespace WolfConquer.Tests;

public sealed class CharPrefixRemainderStartupTests
{
    [Theory]
    [InlineData("", false)]
    [InlineData("/outside.lua", false)]
    [InlineData("\\outside.lua", false)]
    [InlineData("\\\\host\\share\\outside.lua", false)]
    [InlineData("C:/outside.lua", false)]
    [InlineData("C:outside.lua", false)]
    [InlineData("ini/../outside.lua", false)]
    [InlineData("ini\\..\\outside.lua", false)]
    [InlineData("./ini/file.lua", true)]
    [InlineData("ini/file.lua", true)]
    [InlineData("ini\\file.lua", true)]
    [InlineData(".hidden.lua", true)]
    public void RelativeCandidateGuardKeepsRootAndTraversalDecisions(string value, bool expected)
    {
        Assert.Equal(expected, InvokePolicy("IsRelativeClientStartupCandidate", value));
    }

    [Theory]
    [InlineData(".hidden.lua", true)]
    [InlineData("ini/.cache/a.lua", true)]
    [InlineData("ini.oldcompat/a.lua", true)]
    [InlineData("INI.OLDCOMPAT-old/a.lua", true)]
    [InlineData("eNv_Dx8/a.lua", true)]
    [InlineData("ini/visible.lua", false)]
    [InlineData("ini/．hidden.lua", false)]
    [InlineData("ini/file.hidden.lua", false)]
    public void LuaExclusionKeepsOrdinalDotAndCaseInsensitiveNamedPrefixes(string relative, bool expected)
    {
        // Pure path policy only: no directory/file is created or enumerated.
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "wolf-startup-prefix-" + Guid.NewGuid().ToString("N")));
        var candidate = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

        Assert.Equal(expected, InvokePolicy("IsClientStartupLuaCandidateExcluded", root, candidate));
    }

    private static bool InvokePolicy(string name, params object[] arguments)
    {
        var program = typeof(ClientAssetPresenceAuditor).Assembly.GetType("Program", throwOnError: true)!;
        var method = program.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<bool>(method.Invoke(null, arguments));
    }
}
