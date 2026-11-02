using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

internal static class Chinese7937XuanbaoRulesFixture
{
    internal const string ComposeText = "2@@000@@004@@400@@\r\n1@@0@@3@@300@@\r\n";
    internal const string RandomText = "0000000042@@003@@0000000100@@0000000201@@\r\n" +
        "0000000007@@002@@0000000000@@0000000101@@\r\n0000000042@@001@@0000000001@@0000000301@@\r\n";

    // Independent synthetic fixed-profile byte fixtures; no game rows or key files.
    internal static byte[] ComposePacked() => Convert.FromHexString("5f7a12d9c5110c962f74b79be47a9369df1dc2dd67faa49fe02959859ce03738b2d80ab5");
    internal static byte[] RandomPacked() => Convert.FromHexString(
        "1a7548628928cc4667b533833b8055a873d6eff3bb973d76dc1e32193161932b0c9563b4f" +
        "c8d65adf3945d6c7bb6dd7ad5e8fd01d4a78bfe8c1cf34a3d6014654198ad19d54b4f9e9b" +
        "309d44eb20c2e854f54d2fdda1430f20528bc57c92e6334c2768e9de4c658784e0620536edb" +
        "ff38aee56dbe097f6b564646764651414595e");

    internal static byte[] Pack(string text) => ClientItemCatalogCodec.Encode(Encoding.ASCII.GetBytes(text));
    internal static Chinese7937XuanbaoRuleCatalogs Parse(string compose = ComposeText, string random = RandomText) =>
        ParsePacked(Pack(compose), Pack(random));

    internal static Chinese7937XuanbaoRuleCatalogs ParsePacked(byte[] compose, byte[] random)
    {
        Assert.True(Chinese7937XuanbaoRuleCatalogs.TryParsePacked(7937, compose, random,
            SHA256.HashData(compose), SHA256.HashData(random), out var catalogs, out var failure), failure.ToString());
        return Assert.IsType<Chinese7937XuanbaoRuleCatalogs>(catalogs);
    }
}
