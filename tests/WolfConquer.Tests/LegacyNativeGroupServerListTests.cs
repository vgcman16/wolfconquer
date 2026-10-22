using System.Globalization;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Native = WolfConquer.Core.World.LegacyNativeGroupServerList;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeGroupServerListTests
{
    [Fact]
    public void ServerKeepsAllNineMutableNativeFieldsAndNewRowDefaults()
    {
        var fields = typeof(Native.Server).GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new (string, Type)[]
        {
            ("ID", typeof(uint)), ("MapID", typeof(uint)), ("X", typeof(uint)),
            ("Y", typeof(uint)), ("Group", typeof(uint)), ("Name", typeof(string)),
            ("TransferType", typeof(byte)), ("IPAddress", typeof(string)), ("Port", typeof(ushort)),
        }, fields.Select(field => (field.Name, field.FieldType)));
        Assert.All(fields, field => Assert.False(field.IsInitOnly));
        var row = new Native.Server();
        Assert.Equal(0u, row.ID);
        Assert.Equal(0u, row.MapID);
        Assert.Equal(0u, row.X);
        Assert.Equal(0u, row.Y);
        Assert.Equal(0u, row.Group);
        Assert.Null(row.Name);
        Assert.Equal((byte)0, row.TransferType);
        Assert.Equal("", row.IPAddress);
        Assert.Equal((ushort)0, row.Port);
    }

    [Fact]
    public void PublicCacheReferencesCanBeReplacedWithoutCopyingRows()
    {
        using var state = new NativeState();
        var cache = new LegacyNativeDbSafeDictionary<uint, Native.Server>();
        var row = new Native.Server { ID = 7, Name = "MODEL" };
        Native.GroupServers = cache;
        Native.MyServerInfo = row;
        Native.InterServer = row;
        cache.Add(7, row);
        Assert.Same(cache, Native.GroupServers);
        Assert.Same(row, Native.MyServerInfo);
        Assert.Same(row, Native.InterServer);
        Assert.Same(row, Native.GetServer(7));
        row.MapID = 99;
        Assert.Equal(99u, Native.GetServer(7).MapID);
    }

    [Fact]
    public void IdLookupReturnsTheSameMutableRowAndMissingIdReturnsNull()
    {
        using var state = new NativeState();
        var row = new Native.Server { ID = 7, Name = "MODEL" };
        Native.GroupServers.Add(55, row);
        Assert.Same(row, Native.GetServer(7));
        Assert.Null(Native.GetServer(55));
        Assert.Null(Native.GetServer(uint.MaxValue));
    }

    [Fact]
    public void AddressLookupKeepsTheFirstMatchingValuesEnumerationReference()
    {
        using var state = new NativeState();
        var first = new Native.Server { ID = 1, IPAddress = "MODEL-HOST", Port = 9001 };
        var second = new Native.Server { ID = 2, IPAddress = "MODEL-HOST", Port = 9001 };
        var other = new Native.Server { ID = 3, IPAddress = "MODEL-HOST", Port = 9002 };
        Native.GroupServers.Add(1, first);
        Native.GroupServers.Add(2, second);
        Native.GroupServers.Add(3, other);
        var expected = Native.GroupServers.Values.First(row => row.IPAddress == "MODEL-HOST" && row.Port == 9001);
        Assert.Same(expected, Native.GetServer("MODEL-HOST", 9001));
        Assert.Same(other, Native.GetServer("MODEL-HOST", 9002));
        Assert.Null(Native.GetServer("MODEL-MISSING", 9001));
    }

    [Fact]
    public void TransferQueryUsesRowIdAndOwnTransferTypeButNotGroup()
    {
        using var state = new NativeState();
        Native.MyServerInfo = new Native.Server { ID = 1, TransferType = 2, Group = 10 };
        var sameId = new Native.Server { ID = 1, TransferType = 2 };
        var allowed = new Native.Server { ID = 2, TransferType = 2, Group = 999 };
        var wrongType = new Native.Server { ID = 3, TransferType = 3, Group = 10 };
        Native.GroupServers.Add(7, sameId);
        Native.GroupServers.Add(8, allowed);
        Native.GroupServers.Add(9, wrongType);
        Assert.Same(allowed, Assert.Single(Native.CanTransferServers()));
    }

    [Fact]
    public void EmptyTransferQueryDoesNotDereferenceNullOwnServer()
    {
        using var state = new NativeState();
        Native.MyServerInfo = null!;
        Assert.Empty(Native.CanTransferServers());
    }

    [Fact]
    public void NonemptyTransferQueryDoesDereferenceNullOwnServer()
    {
        using var state = new NativeState();
        Native.MyServerInfo = null!;
        Native.GroupServers.Add(1, new Native.Server { ID = 1 });
        Assert.Throws<NullReferenceException>(() => Native.CanTransferServers());
    }

    [Fact]
    public void NullRowsAreNotRepairedByAnyQueryHelper()
    {
        using var state = new NativeState();
        Native.GroupServers.Add(1, null!);
        Native.MyServerInfo = new Native.Server();
        Assert.Throws<NullReferenceException>(() => Native.GetServer(1));
        Assert.Throws<NullReferenceException>(() => Native.GetServer("MODEL", 1));
        Assert.Throws<NullReferenceException>(() => Native.CanTransferServers());
    }

    [Fact]
    public void NullCacheIsNotReplacedByAnyQueryHelper()
    {
        using var state = new NativeState();
        Native.GroupServers = null!;
        Assert.Throws<NullReferenceException>(() => Native.GetServer(1));
        Assert.Throws<NullReferenceException>(() => Native.GetServer("MODEL", 1));
        Assert.Throws<NullReferenceException>(() => Native.CanTransferServers());
    }

    [Fact]
    public void LoadUsesAllElevenNativeColumnsAndSelectsTheActualRowReference()
    {
        using var state = new NativeState();
        state.WriteLines("7 MODEL-LOCAL 1002 10 11 2 99 123 456 MODEL-HOST 9001");
        Native.Load();
        var row = Assert.Single(Native.GroupServers).Value;
        Assert.Equal(7u, row.ID);
        Assert.Equal("MODEL-LOCAL", row.Name);
        Assert.Equal(1002u, row.MapID);
        Assert.Equal(10u, row.X);
        Assert.Equal(11u, row.Y);
        Assert.Equal((byte)2, row.TransferType);
        Assert.Equal(99u, row.Group);
        Assert.Equal("MODEL-HOST", row.IPAddress);
        Assert.Equal((ushort)9001, row.Port);
        Assert.Same(row, Native.MyServerInfo);
        Assert.Null(Native.InterServer);
        Assert.True(Native.GroupServers.Update);
    }

    [Fact]
    public void SingleTokenLineAdmitsTheZeroDefaultRowInsteadOfItsToken()
    {
        using var state = new NativeState();
        state.WriteLines("777");
        Native.Load();
        var row = Native.GroupServers[0];
        Assert.NotNull(row);
        Assert.Equal(0u, row.ID);
        Assert.Equal("", row.Name);
        Assert.Equal("", row.IPAddress);
        Assert.Equal((ushort)0, row.Port);
        Assert.False(Native.GroupServers.ContainsKey(777));
    }

    [Fact]
    public void BlankLineIsNotSkippedAndAdmitsAZeroDefaultRow()
    {
        using var state = new NativeState();
        state.WriteLines("");
        Native.Load();
        var row = Assert.Single(Native.GroupServers).Value;
        Assert.Equal(0u, row.ID);
        Assert.Equal("", row.Name);
        Assert.Equal(0u, row.MapID);
    }

    [Fact]
    public void TabOnlyColumnsDoNotBecomeLiteralSpaceDelimiters()
    {
        using var state = new NativeState();
        state.WriteLines("7\tMODEL-LOCAL\t1002\t10\t11\t2\t99\t123\t456\tMODEL-HOST\t9001");
        Native.Load();
        var row = Assert.Single(Native.GroupServers).Value;
        Assert.Equal(0u, row.ID);
        Assert.Equal("", row.Name);
        Assert.Equal("", row.IPAddress);
    }

    [Fact]
    public void RepeatedSpacesKeepEmptyColumnsInsteadOfCompressingThem()
    {
        using var state = new NativeState();
        state.WriteLines("7 100  200 300 4 5 6 7 MODEL-HOST 9001");
        Native.Load();
        var row = Native.GroupServers[7];
        Assert.Equal("100", row.Name);
        Assert.Equal(0u, row.MapID);
        Assert.Equal(200u, row.X);
        Assert.Equal(300u, row.Y);
        Assert.Equal((byte)4, row.TransferType);
        Assert.Equal(5u, row.Group);
        Assert.Equal("MODEL-HOST", row.IPAddress);
        Assert.Equal((ushort)9001, row.Port);
    }

    [Fact]
    public void InvalidAndOverflowingWidthsUseDefaultsWithoutRejectingTheRow()
    {
        using var state = new NativeState();
        state.WriteLines("8 MODEL 4294967296 -1 4294967295 256 4294967296 BAD BAD MODEL-HOST 65536");
        Native.Load();
        var row = Native.GroupServers[8];
        Assert.Equal(0u, row.MapID);
        Assert.Equal(0u, row.X);
        Assert.Equal(uint.MaxValue, row.Y);
        Assert.Equal((byte)0, row.TransferType);
        Assert.Equal(0u, row.Group);
        Assert.Equal("MODEL-HOST", row.IPAddress);
        Assert.Equal((ushort)0, row.Port);
    }

    [Fact]
    public void ExtraColumnsAfterPortAreIgnored()
    {
        using var state = new NativeState();
        state.WriteLines("7 MODEL 1002 10 11 2 99 123 456 MODEL-HOST 9001 EXTRA TRAILING");
        Native.Load();
        var row = Native.GroupServers[7];
        Assert.Equal("MODEL-HOST", row.IPAddress);
        Assert.Equal((ushort)9001, row.Port);
    }

    [Fact]
    public void RealReaderUsesTheCurrentCulturesCustomPositiveSign()
    {
        using var state = new NativeState();
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = "p";
        culture.NumberFormat.NegativeSign = "n";
        CultureInfo.CurrentCulture = culture;
        state.WriteLines("p9 MODEL p1002 p10 p11 p2 p99 p123 p456 MODEL-HOST p9001");
        Native.Load();
        var row = Native.GroupServers[9];
        Assert.Equal(1002u, row.MapID);
        Assert.Equal(10u, row.X);
        Assert.Equal(11u, row.Y);
        Assert.Equal((byte)2, row.TransferType);
        Assert.Equal(99u, row.Group);
        Assert.Equal((ushort)9001, row.Port);
    }

    [Fact]
    public void NegativeUnsignedAndSignedZeroColumnsKeepDefaultsAndCursorOrder()
    {
        using var state = new NativeState();
        state.WriteLines("-1 MODEL -0 -2 -0 -1 -0 -1 -0 MODEL-HOST -1");
        Native.Load();
        var row = Native.GroupServers[0];
        Assert.Equal(0u, row.ID);
        Assert.Equal(0u, row.MapID);
        Assert.Equal(0u, row.X);
        Assert.Equal(0u, row.Y);
        Assert.Equal((byte)0, row.TransferType);
        Assert.Equal(0u, row.Group);
        Assert.Equal("MODEL-HOST", row.IPAddress);
        Assert.Equal((ushort)0, row.Port);
    }

    [Fact]
    public void DuplicateIdOverwriteCanLeaveAnEarlierSelectedReferenceAlive()
    {
        using var state = new NativeState();
        state.WriteLines(
            "9 MODEL-LOCAL 1 2 3 0 4 5 6 MODEL-A 9001",
            "9 MODEL-OTHER 7 8 9 1 2 3 4 MODEL-B 9002");
        Native.Load();
        var replacement = Assert.Single(Native.GroupServers).Value;
        Assert.Equal("MODEL-OTHER", replacement.Name);
        Assert.Equal("MODEL-B", replacement.IPAddress);
        Assert.NotSame(replacement, Native.MyServerInfo);
        Assert.Equal("MODEL-LOCAL", Native.MyServerInfo.Name);
        Assert.Equal("MODEL-A", Native.MyServerInfo.IPAddress);
        Assert.Same(replacement, Native.GetServer(9));
    }

    [Fact]
    public void LastMatchingOwnAndCaseSensitiveRealmRowsWinWithoutCopies()
    {
        using var state = new NativeState();
        LegacyNativeServerConfig.ServerName = "MODEL-Realm";
        state.WriteLines(
            "1 MODEL-Realm 1 2 3 0 4 5 6 MODEL-A 9001",
            "2 MODEL-Realm 7 8 9 1 2 3 4 MODEL-B 9002",
            "3 MODEL-realm 7 8 9 1 2 3 4 MODEL-C 9003");
        Native.Load();
        Assert.Same(Native.GroupServers[2], Native.MyServerInfo);
        Assert.Same(Native.GroupServers[2], Native.InterServer);
        Assert.NotSame(Native.GroupServers[3], Native.InterServer);
    }

    [Fact]
    public void RepeatedLoadKeepsStaleRowsAndUnmatchedSelectedReferences()
    {
        using var state = new NativeState();
        LegacyNativeServerConfig.ServerName = "MODEL-Realm";
        state.WriteLines("1 MODEL-Realm 1 2 3 0 4 5 6 MODEL-A 9001");
        Native.Load();
        var selected = Native.GroupServers[1];
        state.WriteLines("2 MODEL-OTHER 7 8 9 1 2 3 4 MODEL-B 9002");
        Native.Load();
        Assert.Equal(2, Native.GroupServers.Count);
        Assert.Same(selected, Native.GroupServers[1]);
        Assert.Same(selected, Native.MyServerInfo);
        Assert.Same(selected, Native.InterServer);
    }

    [Fact]
    public void EmptyFilePreservesExistingRowsAndSelectedReferences()
    {
        using var state = new NativeState();
        var old = new Native.Server { ID = 99, Name = "MODEL-OLD" };
        Native.GroupServers.Add(99, old);
        Native.MyServerInfo = old;
        Native.InterServer = old;
        state.WriteLines();
        Native.Load();
        Assert.Same(old, Assert.Single(Native.GroupServers).Value);
        Assert.Same(old, Native.MyServerInfo);
        Assert.Same(old, Native.InterServer);
    }

    [Fact]
    public void MissingFileFaultsBeforeMutatingCacheOrSelectedReferences()
    {
        using var state = new NativeState();
        var old = new Native.Server { ID = 99 };
        Native.GroupServers.Add(99, old);
        Native.MyServerInfo = old;
        Native.InterServer = old;
        state.CreateOwnedDirectory();
        Assert.Throws<FileNotFoundException>(Native.Load);
        Assert.Same(old, Assert.Single(Native.GroupServers).Value);
        Assert.Same(old, Native.MyServerInfo);
        Assert.Same(old, Native.InterServer);
    }

    [Fact]
    public void WholeFileReadPrecedesTheNullCacheFault()
    {
        using var state = new NativeState();
        state.CreateOwnedDirectory();
        Native.GroupServers = null!;
        Assert.Throws<FileNotFoundException>(Native.Load);
        Assert.Null(Native.GroupServers);
        state.WriteLines("7 MODEL 1 2 3 0 4 5 6 MODEL-HOST 9001");
        Assert.Throws<NullReferenceException>(Native.Load);
        Assert.Null(Native.GroupServers);
    }

    [Fact]
    public void NullConfiguredOwnNameDoesNotBlockIndependentRealmSelection()
    {
        using var state = new NativeState();
        LegacyNativeServerConfig.ServerName = null!;
        state.WriteLines("7 MODEL-Realm 1 2 3 0 4 5 6 MODEL-HOST 9001");
        Native.Load();
        Assert.Null(Native.MyServerInfo);
        Assert.Same(Native.GroupServers[7], Native.InterServer);
    }

    private sealed class NativeState : IDisposable
    {
        private readonly LegacyNativeDbSafeDictionary<uint, Native.Server>? _oldGroups = Native.GroupServers;
        private readonly Native.Server? _oldOwn = Native.MyServerInfo;
        private readonly Native.Server? _oldInter = Native.InterServer;
        private readonly string _oldDbLocation = LegacyNativeServerConfig.DbLocation;
        private readonly string _oldName = LegacyNativeServerConfig.ServerName;
        private readonly CultureInfo _oldCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _oldUiCulture = CultureInfo.CurrentUICulture;
        private readonly string _temporaryBase = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        private readonly string _name = "WolfConquer-Goal564-" + Guid.NewGuid().ToString("N");

        public string OwnedDirectory { get; }
        public string FilePath { get; }

        public NativeState()
        {
            OwnedDirectory = Path.GetFullPath(Path.Combine(_temporaryBase, _name));
            FilePath = Path.GetFullPath(Path.Combine(OwnedDirectory, "client_config.ini"));
            ValidateOwnedPaths();
            Native.GroupServers = new LegacyNativeDbSafeDictionary<uint, Native.Server>();
            Native.MyServerInfo = null!;
            Native.InterServer = null!;
            LegacyNativeServerConfig.DbLocation = OwnedDirectory + Path.DirectorySeparatorChar;
            LegacyNativeServerConfig.ServerName = "MODEL-LOCAL";
        }

        public void CreateOwnedDirectory()
        {
            ValidateOwnedPaths();
            Directory.CreateDirectory(OwnedDirectory);
            ValidateOwnedPaths();
        }

        public void WriteLines(params string[] lines)
        {
            Assert.InRange(lines.Length, 0, 4);
            CreateOwnedDirectory();
            File.WriteAllLines(FilePath, lines, new UTF8Encoding(false));
        }

        private void ValidateOwnedPaths()
        {
            Assert.True(Path.IsPathFullyQualified(OwnedDirectory));
            Assert.True(string.Equals(Path.GetDirectoryName(OwnedDirectory), _temporaryBase, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(_name, Path.GetFileName(OwnedDirectory));
            Assert.True(string.Equals(Path.GetDirectoryName(FilePath), OwnedDirectory, StringComparison.OrdinalIgnoreCase));
            Assert.Equal("client_config.ini", Path.GetFileName(FilePath));
            Assert.Equal(0, (int)(new DirectoryInfo(_temporaryBase).Attributes & FileAttributes.ReparsePoint));
            if (Directory.Exists(OwnedDirectory))
                Assert.Equal(0, (int)(new DirectoryInfo(OwnedDirectory).Attributes & FileAttributes.ReparsePoint));
            if (File.Exists(FilePath))
                Assert.Equal(0, (int)(File.GetAttributes(FilePath) & (FileAttributes.ReparsePoint | FileAttributes.Directory)));
        }

        public void Dispose()
        {
            try
            {
                Native.GroupServers = _oldGroups!;
                Native.MyServerInfo = _oldOwn!;
                Native.InterServer = _oldInter!;
                LegacyNativeServerConfig.DbLocation = _oldDbLocation;
                LegacyNativeServerConfig.ServerName = _oldName;
                CultureInfo.CurrentCulture = _oldCulture;
                CultureInfo.CurrentUICulture = _oldUiCulture;
            }
            finally
            {
                ValidateOwnedPaths();
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
                if (Directory.Exists(OwnedDirectory))
                    Directory.Delete(OwnedDirectory, recursive: false);
            }
        }
    }
}
