using System.Buffers.Binary;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyItemUidRegistryTests
{
    [Fact]
    public void QuiescentBootstrapIncludesRawHiddenRowsDepositsMailAndBothDetainedViews()
    {
        using var fixture = new Fixture();
        fixture.SeedItems(11, Fixture.Item(10, [Fixture.Item(50)]), Fixture.Item(100, position: ushort.MaxValue));
        fixture.SeedMail(500, expired: true);
        fixture.SeedDetained(800, alias: true);
        Assert.True(LegacyItemUidRawCatalog.TryScan(fixture.Items, fixture.Mail, fixture.Redeem, fixture.Claim,
            out var observed, out var reason), reason);
        Assert.Equal(800U, observed.Maximum);
        Assert.Equal(4, observed.PhysicalCount); // Hidden DTO row still counts; escrow alias counts once.
        Assert.Equal(1, observed.ReferenceCount);
        using var owner = fixture.Bootstrap(200);
        Assert.Equal(new uint[] { 801 }, Fixture.Reserve(owner, 1));
    }

    [Fact]
    public void MailAndRewardCpsReferencesDoNotInventDuplicatePhysicalItems()
    {
        using var fixture = new Fixture();
        fixture.SeedItems(11, Fixture.Item(400));
        fixture.SeedMail(400);
        fixture.SeedDetained(400, rewardOnly: true);
        Assert.True(LegacyItemUidRawCatalog.TryScan(fixture.Items, fixture.Mail, fixture.Redeem, fixture.Claim,
            out var observed, out var reason), reason);
        Assert.Equal(1, observed.PhysicalCount);
        Assert.Equal(2, observed.ReferenceCount);
        using var owner = fixture.Bootstrap(500);
        Assert.Equal(new uint[] { 501 }, Fixture.Reserve(owner, 1));
    }

    [Theory]
    [InlineData("same-file")]
    [InlineData("foreign-account")]
    [InlineData("deposit")]
    [InlineData("detained")]
    public void RawPhysicalUidDuplicatesCannotDisappearThroughDtoDeduplication(string kind)
    {
        using var fixture = new Fixture();
        var path = fixture.SeedItems(11, Fixture.Item(100));
        switch (kind)
        {
            case "same-file": path = fixture.SeedItems(11, Fixture.Item(100), Fixture.Item(100)); break;
            case "foreign-account": fixture.SeedItems(12, Fixture.Item(100)); break;
            case "deposit": path = fixture.SeedItems(11, Fixture.Item(100, [Fixture.Item(100)])); break;
            case "detained": fixture.SeedDetained(100, alias: true); break;
        }
        var before = File.ReadAllBytes(path);
        Assert.False(LegacyItemUidRegistry.TryBootstrap(fixture.Options(), out var owner, out var reason));
        Assert.Null(owner);
        Assert.StartsWith("itemUidCatalogInvalid:", reason);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.False(File.Exists(fixture.State));
    }

    [Theory]
    [InlineData("negative-count")]
    [InlineData("huge-count")]
    [InlineData("truncated")]
    [InlineData("trailing")]
    [InlineData("zero-uid")]
    [InlineData("zero-item")]
    [InlineData("nested-deposit")]
    [InlineData("foreign-perfection")]
    public void MalformedRawItemFilesRefuseWithoutRepairOrPartialRegistryPublication(string kind)
    {
        using var fixture = new Fixture();
        var path = fixture.SeedItems(11, Fixture.Item(100));
        var bytes = File.ReadAllBytes(path);
        switch (kind)
        {
            case "negative-count": BinaryPrimitives.WriteInt32LittleEndian(bytes, -1); break;
            case "huge-count": BinaryPrimitives.WriteInt32LittleEndian(bytes, int.MaxValue); break;
            case "truncated": bytes = bytes[..^1]; break;
            case "trailing": bytes = [.. bytes, 99]; break;
            case "zero-uid": BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0); break;
            case "zero-item": BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 0); break;
            case "nested-deposit":
                path = fixture.SeedItems(11, Fixture.Item(100, [Fixture.Item(101, [Fixture.Item(102)])]));
                bytes = File.ReadAllBytes(path); break;
            case "foreign-perfection":
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(bytes.Length - 4), 1);
                bytes = [.. bytes, .. new byte[PlayerItemSnapshotLoader.LegacyPerfectionSize]];
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(bytes.Length - PlayerItemSnapshotLoader.LegacyPerfectionSize), 999);
                break;
        }
        File.WriteAllBytes(path, bytes);
        Assert.False(LegacyItemUidRegistry.TryBootstrap(fixture.Options(), out _, out _));
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.False(File.Exists(fixture.State));
    }

    [Theory]
    [InlineData("conflicting-escrow")]
    [InlineData("duplicate-escrow-row")]
    [InlineData("unknown-action")]
    [InlineData("mail-size")]
    [InlineData("mail-duplicate")]
    [InlineData("owner-alias")]
    [InlineData("unknown-file")]
    public void AmbiguousExternalReferencesAndStoreShapesAreNeverSilentlyIgnored(string kind)
    {
        using var fixture = new Fixture();
        fixture.SeedItems(11, Fixture.Item(100));
        fixture.SeedMail(200);
        fixture.SeedDetained(300, alias: true);
        var mail = Path.Combine(fixture.Mail, "11.bin");
        switch (kind)
        {
            case "conflicting-escrow":
                var altered = File.ReadAllBytes(fixture.Claim); altered[^1] = 1;
                File.WriteAllBytes(fixture.Claim, altered); break;
            case "duplicate-escrow-row":
                var detained = File.ReadAllBytes(fixture.Redeem);
                detained = [.. detained, .. detained.AsSpan(4).ToArray()];
                BinaryPrimitives.WriteInt32LittleEndian(detained, 2);
                File.WriteAllBytes(fixture.Redeem, detained); break;
            case "unknown-action":
                var action = File.ReadAllBytes(fixture.Claim);
                BinaryPrimitives.WriteUInt32LittleEndian(action.AsSpan(4 + 16), 99);
                File.WriteAllBytes(fixture.Claim, action); break;
            case "mail-size": File.AppendAllText(mail, "x"); break;
            case "mail-duplicate":
                var mails = File.ReadAllBytes(mail);
                mails = [.. mails, .. mails.AsSpan(4).ToArray()];
                BinaryPrimitives.WriteInt32LittleEndian(mails, 2);
                File.WriteAllBytes(mail, mails); break;
            case "owner-alias": File.Move(Path.Combine(fixture.Items, "11.bin"), Path.Combine(fixture.Items, "011.bin")); break;
            case "unknown-file": File.WriteAllText(Path.Combine(fixture.Items, "unknown.txt"), "not an item store"); break;
        }
        Assert.False(LegacyItemUidRegistry.TryBootstrap(fixture.Options(), out _, out _));
        Assert.False(File.Exists(fixture.State));
    }
}
