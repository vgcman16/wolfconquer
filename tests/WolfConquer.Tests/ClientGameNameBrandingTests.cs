using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientGameNameBrandingTests
{
    [Theory]
    [InlineData("Conquer", true)]
    [InlineData("Rayzo", true)]
    [InlineData("WolfConquer", false)]
    public void ExplicitOwnedBrandCatalogChangesOnlyTheDefaultDisplayValue(string originalName, bool changed)
    {
        // This is a newly authored explicit operation, not a bundled translation dataset.
        var patch = new ClientPatchFile("ini/ndloginServer.dat", ClientTextEncodingKind.CodePage936,
            [new("Default.GameName", "WolfConquer")]);
        var encoding = ClientTextEncodings.GetEncoding(ClientTextEncodingKind.CodePage936);
        const string prefix = ";keep 中\r\n[Default]\r\n GameName = ";
        const string suffix = "\r\nServerName=Rayzo\r\nExecutable=Conquer.exe\r\n[Other]\nGameName=Conquer\rTail=Razo";
        var source = encoding.GetBytes(prefix + originalName + suffix);
        var preserved = source.ToArray();
        var expected = encoding.GetBytes(prefix + "WolfConquer" + suffix);
        var candidate = EnglishClientPatch.BuildFileCandidate(patch, source);
        Assert.Equal(new ClientPatchFileResult(patch.RelativePath, 1, 0, changed), candidate.Result);
        Assert.Equal(preserved, source);
        Assert.Equal(expected, candidate.ToArray());
        var repeat = EnglishClientPatch.BuildFileCandidate(patch, candidate.ToArray());
        Assert.False(repeat.Result.Changed);
        Assert.Equal(expected, repeat.ToArray());
    }
}
