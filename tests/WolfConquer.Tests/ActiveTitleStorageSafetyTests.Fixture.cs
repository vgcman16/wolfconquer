using System.Buffers;
using System.Globalization;
using System.Net;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ActiveTitleStorageSafetyTests
{
    private const uint WingId = 4_001;
    private const uint WingSubId = 17;
    private const string PackedWing = "40010017";
    private const string NormalTitle = "20010023";
    private const string NormalScore = "73";
    private const string InactiveWingTitles = "3,2001,1,4001,0,4002,0,";
    private const string ActiveWingTitles = "3,2001,1,4001,1,4002,0,";

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out var consumed, out _));
        Assert.Equal(sequence.Length, sequence.Slice(0, consumed).Length);
        return frame;
    }

    private static LegacyTitleStoragePacket ReadTitlePacket(byte[] bytes)
    {
        Assert.True(LegacyTitleStoragePacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static void AssertPeerSpawn(byte[] bytes, ulong wing)
    {
        Assert.True(LegacyPlayerSpawnPacket.TryParse(ReadFrame(bytes), out var spawn));
        Assert.Equal((ulong)Fixture.CharacterUid, spawn.GetNumericField(2));
        Assert.Equal(20_010_023UL, spawn.GetNumericField(89));
        Assert.Equal(73UL, spawn.GetNumericField(90));
        Assert.Equal(wing, spawn.GetNumericField(91));
    }

    private sealed class Fixture : IDisposable
    {
        internal const long CharacterUid = 1_293_600;
        private const long DifferentAccountId = 88_220;
        private readonly string _temporaryParent = Path.GetFullPath(Path.GetTempPath());
        private readonly string _root;
        private readonly string _catalogPath;
        private readonly string _decoyPath;
        private readonly PacketHandlerRegistry<WorldPacketHandlerContext> _registry =
            PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        internal Fixture(bool equipped = false, bool differentAccountId = false)
        {
            _root = Path.GetFullPath(Path.Combine(_temporaryParent,
                "wolf-active-title-safety-" + Guid.NewGuid().ToString("N")));
            ValidateRoot();
            RequireNoReparsePoint(_temporaryParent);
            Assert.False(Directory.Exists(_root));
            Directory.CreateDirectory(_root);
            RequireNoReparsePoint(_root);
            UsersRoot = Path.Combine(_root, "Users");
            Directory.CreateDirectory(UsersRoot);
            RequireNoReparsePoint(UsersRoot);
            Recovery = new LegacyTradeRecoveryState(UsersRoot, null);
            _catalogPath = Path.Combine(_root, "title_type.txt");
            SnapshotPath = Path.Combine(UsersRoot, CharacterUid.ToString(CultureInfo.InvariantCulture) + ".ini");
            _decoyPath = Path.Combine(UsersRoot, DifferentAccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
            WriteCatalog(
                "2001@@23@@Normal@@0@@0@@0@@0@@73@@",
                "4001@@17@@Wing@@0@@0@@0@@0@@91@@",
                "4002@@19@@OtherWing@@0@@0@@0@@0@@92@@");
            var text = SnapshotText(CharacterUid, equipped);
            File.WriteAllText(SnapshotPath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Original = new WorldSession(
                differentAccountId ? DifferentAccountId : CharacterUid,
                "synthetic-title-owner",
                CharacterSnapshotParser.ParseText(text, SnapshotPath),
                PlayerItemSnapshot.Empty,
                PlayerSpellSnapshot.Empty,
                PlayerProficiencySnapshot.Empty,
                UsersRoot: UsersRoot);
            Connection = CreateConnection(Sent);
            PeerConnection = CreateConnection(PeerSent);
            Sessions.Set(Connection, Original);
            Sessions.Set(PeerConnection, Original with
            {
                AccountId = CharacterUid + 1,
                Username = "synthetic-visible-peer",
                Character = Original.Character with { Uid = CharacterUid + 1, Name = "TitlePeer", X = 501 },
            });
        }

        internal string UsersRoot { get; }
        internal string SnapshotPath { get; }
        internal WorldSession Original { get; }
        internal WorldSessionStore Sessions { get; } = new();
        internal LegacyTradeRecoveryState Recovery { get; }
        internal ConquerTcpConnection Connection { get; }
        internal ConquerTcpConnection PeerConnection { get; }
        internal List<byte[]> Sent { get; } = [];
        internal List<byte[]> PeerSent { get; } = [];
        internal StringWriter Log { get; } = new();
        internal bool VerifyPersistenceBeforePublication { get; set; }
        internal Action? AfterOwnerFrameSent { get; set; }

        internal WorldPacketHandlerContext Context => new(Connection, Log, Original, Sessions: Sessions)
        {
            TradeRecovery = Recovery,
        };

        internal async Task DispatchAsync(LegacyTitleStorageAction action = LegacyTitleStorageAction.Equip,
            uint id = WingId, uint subtype = WingSubId,
            LegacyTitleStorageTitle? nestedTitle = null,
            WorldPacketHandlerContext? context = null, CancellationToken cancellationToken = default)
        {
            var request = new LegacyTitleStoragePacket(action, DwParam1: 0, DwParam2: id, DwParam3: subtype,
                Title: nestedTitle);
            await _registry.HandleAsync(context ?? Context, ReadFrame(request.ToFrameBytes()), cancellationToken);
        }

        internal WorldSession Current()
        {
            Assert.True(Sessions.TryGet(Connection, out var session));
            return Assert.IsType<WorldSession>(session);
        }

        internal CharacterSnapshot ReadPersisted() =>
            CharacterSnapshotParser.ParseText(File.ReadAllText(SnapshotPath), SnapshotPath);

        internal void AssertRejected(WorldSession expected, byte[]? before)
        {
            Assert.Same(expected, Current());
            Assert.Empty(Sent);
            Assert.Empty(PeerSent);
            if (before is null)
                Assert.False(File.Exists(SnapshotPath));
            else
                Assert.Equal(before, File.ReadAllBytes(SnapshotPath));
        }

        internal void WriteCatalog(params string[] rows) =>
            File.WriteAllLines(_catalogPath, rows, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        internal void ReplaceStoredUid(long uid, bool equipped) =>
            File.WriteAllText(SnapshotPath, SnapshotText(uid, equipped),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        internal byte[] WriteAccountDecoy()
        {
            File.WriteAllText(_decoyPath, SnapshotText(DifferentAccountId, equipped: false),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return File.ReadAllBytes(_decoyPath);
        }

        internal byte[] ReadAccountDecoy() => File.ReadAllBytes(_decoyPath);

        internal WorldSession ConfigureOwnedTitle(uint id)
        {
            var text = SnapshotText(CharacterUid, equipped: false).Replace(
                "SpecialTitles=" + InactiveWingTitles,
                "SpecialTitles=1," + id.ToString(CultureInfo.InvariantCulture) + ",0,",
                StringComparison.Ordinal);
            File.WriteAllText(SnapshotPath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            var session = Original with { Character = CharacterSnapshotParser.ParseText(text, SnapshotPath) };
            Sessions.Set(Connection, session);
            return session;
        }

        private ConquerTcpConnection CreateConnection(List<byte[]> frames) => new(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (bytes, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (VerifyPersistenceBeforePublication)
                {
                    var current = Current();
                    Assert.NotSame(Original, current);
                    Assert.Equal(CharacterUid, ReadPersisted().Uid);
                    AssertTitleAttributesEqual(current.Character, ReadPersisted());
                }
                frames.Add(bytes.ToArray());
                if (ReferenceEquals(frames, Sent)) AfterOwnerFrameSent?.Invoke();
                return ValueTask.CompletedTask;
            });

        private void ValidateRoot()
        {
            var prefix = Path.TrimEndingDirectorySeparator(_temporaryParent) + Path.DirectorySeparatorChar;
            Assert.True(_root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(Path.TrimEndingDirectorySeparator(_temporaryParent), Path.GetDirectoryName(_root));
            const string marker = "wolf-active-title-safety-";
            var name = Path.GetFileName(_root);
            Assert.StartsWith(marker, name);
            Assert.True(Guid.TryParseExact(name[marker.Length..], "N", out _));
        }

        private static void RequireNoReparsePoint(string path)
        {
            if (File.Exists(path) || Directory.Exists(path))
                Assert.Equal((FileAttributes)0, File.GetAttributes(path) & FileAttributes.ReparsePoint);
        }

        public void Dispose()
        {
            Log.Dispose();
            ValidateRoot();
            RequireNoReparsePoint(_temporaryParent);
            RequireNoReparsePoint(_root);
            RequireNoReparsePoint(UsersRoot);
            // Only the three directly known fixture files may be deleted. Unknown leftovers fail
            // the subsequent nonrecursive directory deletion instead of broadening cleanup.
            foreach (var file in new[] { SnapshotPath, _decoyPath, _catalogPath })
            {
                var full = Path.GetFullPath(file);
                Assert.True(full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
                RequireNoReparsePoint(full);
                if (File.Exists(full)) File.Delete(full);
            }
            if (Directory.Exists(UsersRoot)) Directory.Delete(UsersRoot, recursive: false);
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: false);
        }

        private static string SnapshotText(long uid, bool equipped) => $$"""
            ; owned synthetic title test save - preserve unrelated content
            [Character]
            UID={{uid.ToString(CultureInfo.InvariantCulture)}}
            Name=TitleOwner
            Body=1008
            Face=154
            Haire=27
            Class=1005
            FirstClass=0
            SecoundeClass=0
            Reborn=0
            Level=50
            Experience=0
            Map=1002
            X=500
            Y=650
            PMap=1002
            PMapX=500
            PMapY=650
            Strength=30
            Agility=20
            Vitaliti=40
            Spirit=10
            Atributes=0
            MinHitPoints=800
            MinMana=300
            Money=12345
            ConquerPoints=678
            BoundConquerPoints=0
            BankCps=0
            PkPoints=0
            MyTitle=9
            SpecialTitleID={{NormalTitle}}
            SpecialTitleScore={{NormalScore}}
            SpecialWingID={{(equipped ? PackedWing : "0")}}
            SpecialTitles={{(equipped ? ActiveWingTitles : InactiveWingTitles)}}
            UnrelatedSentinel=retain-me
            Online=1
            [UnrelatedSection]
            Marker=retained
            """;
    }

    private static void AssertTitleAttributesEqual(CharacterSnapshot expected, CharacterSnapshot actual)
    {
        foreach (var key in new[] { "SpecialTitleID", "SpecialTitleScore", "SpecialWingID", "SpecialTitles", "MyTitle" })
            Assert.Equal(expected.Attributes[key], actual.Attributes[key]);
        Assert.Equal("retain-me", actual.Attributes["UnrelatedSentinel"]);
    }
}
