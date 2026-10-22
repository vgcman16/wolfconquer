using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Attr = WolfConquer.Core.Data.LegacyNativeHundredWeaponsTable.AttributeType;
using Weapon = WolfConquer.Core.Data.LegacyNativeMagicType.WeaponsType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHundredWeaponsTests
{
    private sealed class Files : IDisposable
    {
        private readonly string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "WolfConquer-Hundred-" + Guid.NewGuid().ToString("N")));
        internal string Prefix => Path.Combine(root, "raw-prefix-");
        internal string PathName => Prefix + "hundred_weapon.txt";
        internal Files() => Directory.CreateDirectory(root);
        internal void Write(params string[] lines) => File.WriteAllLines(PathName, lines);
        public void Dispose()
        {
            string expectedParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!root.StartsWith(expectedParent, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(root).StartsWith("WolfConquer-Hundred-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing to remove a non-owned test directory.");
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private const string MetadataRow = "17@@410@@4@@9@@4294967295@@10007@@20008@@30009@@12000@@2@@1";

    [Fact]
    public Task MetadataLoadUsesExactRawPrefixAllColumnsAndConcreteDuplicateFailureWithoutClearing() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new Files(); LegacyNativeServerConfig.DbLocation = files.Prefix;
        var dictionary = scope.Metadata; files.Write(MetadataRow); LegacyNativeHundredWeaponsTable.Load();
        Assert.Same(dictionary, scope.Metadata); var row = Assert.Single(dictionary).Value;
        Assert.Equal(Weapon.Blade, row.WeaponSubtype); Assert.Equal((byte)4, row.Level); Assert.Equal((byte)9, row.WeaponProf);
        Assert.Equal(uint.MaxValue, row.Progress); Assert.Equal((ushort)12000, row.MagicType); Assert.Equal((byte)2, row.ReqItemCount); Assert.True(row.CanPolish);
        Assert.Equal(new int[] { 7, 8, 9, 0, 0 }, new[] { row.Attributes[Attr.Hitpoints], row.Attributes[Attr.PhysicalAttack], row.Attributes[Attr.PhysicalDefense], row.Attributes[Attr.MagicAttack], row.Attributes[Attr.MagicDefense] });
        Assert.Throws<ArgumentException>(() => LegacyNativeHundredWeaponsTable.Load());
        Assert.Same(row, Assert.Single(dictionary).Value); Assert.True(File.Exists(files.PathName));
    });

    [Fact]
    public Task MissingMetadataFileRetainsExistingPhysicalDictionaryAndRows() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new Files(); LegacyNativeServerConfig.DbLocation = files.Prefix;
        var row = scope.Meta(Weapon.Sword, 9); var dictionary = scope.Metadata;
        LegacyNativeHundredWeaponsTable.Load();
        Assert.Same(dictionary, scope.Metadata); Assert.Same(row, Assert.Single(dictionary).Value); Assert.False(File.Exists(files.PathName));
    });

    [Theory]
    [InlineData("18@@410@@4@@9@@0@@10001@@10002@@30003@@12000@@1@@1", 0)]
    [InlineData("18@@410@@256@@9@@0@@10001@@20002@@30003@@12000@@1@@1", 1)]
    [InlineData("18@@410", 2)]
    public Task MalformedLaterMetadataRowPreservesOnlyFullyPublishedPrefix(string malformed, int kind) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new Files(); LegacyNativeServerConfig.DbLocation = files.Prefix;
        files.Write(MetadataRow, malformed);
        if (kind == 0) Assert.Throws<ArgumentException>(() => LegacyNativeHundredWeaponsTable.Load());
        else if (kind == 1) Assert.Throws<OverflowException>(() => LegacyNativeHundredWeaponsTable.Load());
        else Assert.Throws<IndexOutOfRangeException>(() => LegacyNativeHundredWeaponsTable.Load());
        Assert.Equal(17u, Assert.Single(scope.Metadata).Key); Assert.Equal((byte)4, scope.Metadata[17].Level);
        Assert.False(scope.Metadata.ContainsKey(18));
    });

    [Fact]
    public Task MetadataConversionsUseCurrentCultureSignedRemaindersByteNarrowingAndExactBooleanOne() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new Files(); LegacyNativeServerConfig.DbLocation = files.Prefix;
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone(); culture.NumberFormat.PositiveSign = "p";
        CultureInfo.CurrentCulture = culture;
        files.Write("p18@@p410@@p4@@p9@@p7@@-10001@@10002@@20003@@p12000@@p2@@p2");
        LegacyNativeHundredWeaponsTable.Load(); var row = scope.Metadata[18];
        Assert.Equal(Weapon.Blade, row.WeaponSubtype); Assert.Equal(7u, row.Progress); Assert.False(row.CanPolish);
        Assert.Equal(-1, row.Attributes[(Attr)255]); Assert.Equal(2, row.Attributes[Attr.Hitpoints]);
        Assert.Equal(3, row.Attributes[Attr.PhysicalAttack]); Assert.Equal(0, row.Attributes[Attr.MagicAttack]); Assert.Equal(0, row.Attributes[Attr.MagicDefense]);
        Assert.False(row.Attributes.ContainsKey(Attr.PhysicalDefense));
    });
}
