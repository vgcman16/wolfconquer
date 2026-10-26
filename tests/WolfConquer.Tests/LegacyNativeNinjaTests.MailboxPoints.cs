using System.Buffers.Binary;
using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaTests
{
    private const string MailboxNinjaLine =
        "1/25/2/3/4/5/6/7/8/9/10/11/777/12/13/14/15/16/17/2/100/2/1/31/41/404/3/9/32/42/";

    [Fact]
    public void MailboxPointsProjectionPreservesCanonicalLineAndSourceGoldenBeforeExplicitPublication()
    {
        using var tables = new Tables();
        var (client, ninja) = MailboxNinja();
        LegacyNativeNinjaFile.gouyu_type = null!; // Stored Score is retained; no optional metadata query.
        var before = MailboxNinjaState(ninja);
        Assert.True(ninja.TryPrepareMailboxPoints(client, MailboxNinjaLine, 50, out var projection));
        Assert.NotNull(projection);
        Assert.Equal(MailboxNinjaLine, projection.BeforeText);
        Assert.Equal(MailboxNinjaLine.Replace("1/25/", "1/75/", StringComparison.Ordinal), projection.AfterText);
        Assert.Equal(75U, projection.AfterExp);
        Assert.Equal(before, MailboxNinjaState(ninja));
        Assert.True(projection.IsCurrent());
        Assert.False(projection.IsPublishedCurrent());
        // Source protobuf-net2 required-zero contract, Type2/Points75, no native send.
        byte[] payload = Convert.FromHexString(
            "080210001800204B28003000380040004800500058006000680070007800800100880100900100980100A00100A80100");
        var expected = new byte[payload.Length + 4];
        BinaryPrimitives.WriteUInt16LittleEndian(expected, checked((ushort)expected.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2), 2395);
        payload.CopyTo(expected, 4);
        Assert.Equal(expected, projection.CopyFrame());
        projection.CopyFrame().AsSpan().Fill(0xA5);
        Assert.Equal(expected, projection.CopyFrame());
        Assert.True(projection.TryPublishCommitted()); // Component publication only; no durability claim.
        Assert.Equal(75U, ninja.Exp);
        Assert.Equal(before.Replace("Exp=25;", "Exp=75;", StringComparison.Ordinal), MailboxNinjaState(ninja));
        Assert.False(projection.IsCurrent());
        Assert.True(projection.IsPublishedCurrent());
        Assert.False(projection.TryPublishCommitted());
        Assert.Equal(75U, ninja.Exp);
        Assert.True(projection.IsPublishedCurrent());
        Assert.Null(client.Socket); // No socket/queue substitute is created for a pure projection.
    }

    [Theory]
    [InlineData("missing-trailer")]
    [InlineData("truncated-row")]
    [InlineData("extra-tail")]
    [InlineData("duplicate-row")]
    [InlineData("wrong-row")]
    [InlineData("wrong-count")]
    [InlineData("wrong-scalar")]
    [InlineData("noncanonical")]
    [InlineData("negative-score")]
    [InlineData("overflow-score")]
    [InlineData("empty")]
    [InlineData("null")]
    public void MailboxPointsMalformedOrMismatchedSavedLineRefusesWithoutAnyNativeMutation(string fault)
    {
        using var tables = new Tables();
        var (client, ninja) = MailboxNinja();
        var parts = MailboxNinjaLine.Split('/');
        string? line = MailboxNinjaLine;
        switch (fault)
        {
            case "missing-trailer": line = line[..^1]; break;
            case "truncated-row": line = string.Join('/', parts.Take(25)) + "/"; break;
            case "extra-tail": line += "0/"; break;
            case "duplicate-row": parts[25] = "100"; line = string.Join('/', parts); break;
            case "wrong-row": parts[29] = "43"; line = string.Join('/', parts); break;
            case "wrong-count": parts[19] = "1"; line = string.Join('/', parts); break;
            case "wrong-scalar": parts[18] = "18"; line = string.Join('/', parts); break;
            case "noncanonical": parts[1] = "025"; line = string.Join('/', parts); break;
            case "negative-score": parts[12] = "-1"; line = string.Join('/', parts); break;
            case "overflow-score": parts[12] = "4294967296"; line = string.Join('/', parts); break;
            case "empty": line = ""; break;
            case "null": line = null; break;
            default: throw new ArgumentOutOfRangeException(nameof(fault));
        }
        var before = MailboxNinjaState(ninja);
        Assert.False(ninja.TryPrepareMailboxPoints(client, line!, 50, out var projection));
        Assert.Null(projection);
        Assert.Equal(before, MailboxNinjaState(ninja));
        Assert.Null(client.Socket);
    }

    [Theory]
    [InlineData(0U, 25U)]
    [InlineData(1U, uint.MaxValue)]
    public void MailboxPointsZeroOrOverflowRefusesBeforeNativeMutation(uint points, uint exp)
    {
        using var tables = new Tables();
        var (client, ninja) = MailboxNinja();
        ninja.Exp = exp;
        var line = MailboxNinjaLine.Replace("1/25/", "1/" + exp + "/", StringComparison.Ordinal);
        var before = MailboxNinjaState(ninja);
        Assert.False(ninja.TryPrepareMailboxPoints(client, line, points, out var projection));
        Assert.Null(projection);
        Assert.Equal(before, MailboxNinjaState(ninja));
        Assert.Null(client.Socket);
    }

    private static (LegacyNativeGameClientRuntime Client, LegacyNativeNinja Ninja) MailboxNinja()
    {
        var client = Client(5051);
        client.Player.UID = 1_293_600;
        var ninja = new LegacyNativeNinja(client);
        client.MyNinja = ninja;
        ninja.Load(MailboxNinjaLine);
        return (client, ninja);
    }

    private static string MailboxNinjaState(LegacyNativeNinja ninja)
    {
        var scalars = typeof(LegacyNativeNinja).GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Where(field => field.Name is not ("Items" or "user")).OrderBy(field => field.MetadataToken)
            .Select(field => field.Name + "=" + Convert.ToUInt64(field.GetValue(ninja)));
        IEnumerable<string> rows = ninja.Items is null ? ["Items=null"] : ninja.Items.OrderBy(row => row.Key).Select(row =>
            row.Value is null ? $"{row.Key}:null" :
                $"{row.Key}:{row.Value.ItemID},{row.Value.Level},{row.Value.Position},{row.Value.Exp},{row.Value.UK}");
        return string.Join(';', scalars.Concat(rows));
    }
}
