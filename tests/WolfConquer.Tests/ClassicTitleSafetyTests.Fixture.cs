using System.Buffers;
using System.Globalization;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class ClassicTitleSafetyTests
{
    private static async Task InFixture(Func<Fixture, Task> body, int encodingKind = 0,
        string titles = "2,5,7", string? selected = "2")
    {
        Fixture? fixture = null;
        Exception? primary = null;
        try { fixture = new(encodingKind, titles, selected); await body(fixture); }
        catch (Exception ex) { primary = ex; }
        try { fixture?.Dispose(); }
        catch (Exception cleanup)
        {
            if (primary is not null) throw new AggregateException("Test and guarded cleanup both failed.", primary, cleanup);
            throw;
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
    }

    private static PacketFrame Frame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private sealed class Fixture : IDisposable
    {
        internal const long Uid = 0x01020304;
        private readonly string temporaryParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        private readonly string root;
        private readonly Encoding encoding;
        private readonly PacketHandlerRegistry<WorldPacketHandlerContext> registry =
            PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        internal Fixture(int encodingKind, string titles, string? selected)
        {
            root = Path.GetFullPath(Path.Combine(temporaryParent, "wolf-classic-title-" + Guid.NewGuid().ToString("N")));
            UsersRoot = Path.Combine(root, "Users");
            SnapshotPath = Path.Combine(UsersRoot, Uid.ToString(CultureInfo.InvariantCulture) + ".ini");
            encoding = EncodingFor(encodingKind);
            ValidateRoot();
            RequireNoReparse(temporaryParent);
            if (Directory.Exists(root)) throw new IOException("Owned fixture already exists.");
            try
            {
                Directory.CreateDirectory(root);
                Directory.CreateDirectory(UsersRoot);
                var text = SnapshotText(titles, selected);
                File.WriteAllText(SnapshotPath, text, encoding);
                Original = new(Uid, "synthetic-classic-title", CharacterSnapshotParser.ParseText(text, SnapshotPath),
                    PlayerItemSnapshot.Empty, PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty, UsersRoot: UsersRoot);
                Recovery = new(UsersRoot, null);
                Connection = new(null, new IPEndPoint(IPAddress.Loopback, 9958), (bytes, cancellationToken) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (VerifyCommittedBeforeSend)
                    {
                        var current = Current();
                        Assert.NotSame(Original, current);
                        Assert.Equal(current.Character.Attributes["MyTitle"], ReadPersisted().Attributes["MyTitle"]);
                    }
                    if (SendFailure is { } failure) throw failure;
                    Sent.Add(bytes.ToArray());
                    AfterSend?.Invoke();
                    return ValueTask.CompletedTask;
                });
                Sessions.Set(Connection, Original);
            }
            catch (Exception primary)
            {
                try { Dispose(); }
                catch (Exception cleanup) { throw new AggregateException("Fixture birth and cleanup both failed.", primary, cleanup); }
                throw;
            }
        }

        internal string UsersRoot { get; }
        internal string SnapshotPath { get; }
        internal WorldSession Original { get; }
        internal WorldSessionStore Sessions { get; } = new();
        internal LegacyTradeRecoveryState Recovery { get; }
        internal ConquerTcpConnection Connection { get; }
        internal List<byte[]> Sent { get; } = [];
        internal StringWriter Log { get; } = new();
        internal bool VerifyCommittedBeforeSend { get; set; }
        internal Exception? SendFailure { get; set; }
        internal Action? AfterSend { get; set; }
        internal WorldPacketHandlerContext Context => new(Connection, Log, Original, Sessions: Sessions)
        { TradeRecovery = Recovery };

        internal Task Dispatch(LegacyTitleAction action = LegacyTitleAction.Change, byte title = 7,
            WorldPacketHandlerContext? context = null, CancellationToken cancellationToken = default) =>
            registry.HandleAsync(context ?? Context,
                Frame(new LegacyTitlePacket(999, title, action).ToFrameBytes()), cancellationToken).AsTask();

        internal Task DispatchBytes(byte[] bytes) => registry.HandleAsync(Context, Frame(bytes)).AsTask();
        internal WorldSession Current()
        {
            Assert.True(Sessions.TryGet(Connection, out var current));
            return Assert.IsType<WorldSession>(current);
        }
        internal CharacterSnapshot ReadPersisted() => CharacterSnapshotParser.ParseText(
            CharacterSnapshotFileEncoding.ReadAllText(SnapshotPath, out _), SnapshotPath);
        internal void Rewrite(Func<string, string> update) => File.WriteAllText(SnapshotPath,
            update(CharacterSnapshotFileEncoding.ReadAllText(SnapshotPath, out _)), encoding);
        internal void Reject(WorldSession expected, byte[]? before)
        {
            Assert.Same(expected, Current());
            Assert.Empty(Sent);
            if (before is null) Assert.False(File.Exists(SnapshotPath));
            else Assert.Equal(before, File.ReadAllBytes(SnapshotPath));
        }
        internal static WorldSession WithSelection(WorldSession original, string value) => original with
        { Character = original.Character with { Attributes = new Dictionary<string, string>(original.Character.Attributes,
            StringComparer.OrdinalIgnoreCase) { ["MyTitle"] = value } } };

        internal static Encoding EncodingFor(int kind)
        {
            if (kind != 2) return new UTF8Encoding(kind == 1);
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252);
        }

        private static string SnapshotText(string titles, string? selected) => $$"""
            ; owned classic-title fixture, not a game save
            [Character]
            UID={{Uid.ToString(CultureInfo.InvariantCulture)}}
            Name=Crème€
            Body=1008
            Face=154
            Haire=27
            Class=1005
            FirstClass=0
            SecoundeClass=0
            Reborn=2
            Level=50
            Experience=0
            Map=1002
            X=500
            Y=650
            PMap=1002
            PMapX=500
            PMapY=650
            Strength=10
            Agility=10
            Vitaliti=10
            Spirit=10
            Atributes=0
            Money=12345
            ConquerPoints=678
            BoundConquerPoints=0
            BankCps=0
            PkPoints=0
            MinHitPoints=800
            MinMana=300
            Online=1
            {{(selected is null ? "; MyTitle is absent" : "MyTitle=" + selected)}}
            Titles={{titles}}
            SpecialTitles=1,4001,1,
            SpecialTitleID=20010023
            SpecialTitleScore=73
            SpecialWingID=40010017
            SecurityPass=91357,0,0,
            UnrelatedSentinel=retain-me
            [UnrelatedSection]
            Sentinel=café€
            """;

        private void ValidateRoot()
        {
            const string marker = "wolf-classic-title-";
            var name = Path.GetFileName(root);
            if (!string.Equals(Path.GetDirectoryName(root), temporaryParent, PathComparison) ||
                !name.StartsWith(marker, StringComparison.Ordinal) || !Guid.TryParseExact(name[marker.Length..], "N", out _))
                throw new IOException("Owned fixture containment changed; preserved.");
        }
        private static StringComparison PathComparison => OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        private static void RequireNoReparse(string path)
        {
            if ((File.Exists(path) || Directory.Exists(path)) &&
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Owned fixture path is a reparse point; preserved.");
        }
        public void Dispose()
        {
            var failures = new List<Exception>();
            void Attempt(Action cleanup)
            {
                try { cleanup(); }
                catch (Exception ex) { failures.Add(ex); }
            }
            try
            {
                ValidateRoot();
                RequireNoReparse(temporaryParent);
                RequireNoReparse(root);
                RequireNoReparse(UsersRoot);
                var full = Path.GetFullPath(SnapshotPath);
                if (!string.Equals(Path.GetDirectoryName(full), UsersRoot, PathComparison))
                    throw new IOException("Owned file containment changed; preserved.");
                RequireNoReparse(full);
                if (File.Exists(full)) Attempt(() => File.Delete(full));
                // Only this directly known file is removed. Unknown transaction
                // leftovers remain and make nonrecursive cleanup report a failure.
                if (Directory.Exists(UsersRoot)) Attempt(() => Directory.Delete(UsersRoot, recursive: false));
                if (Directory.Exists(root)) Attempt(() => Directory.Delete(root, recursive: false));
            }
            catch (Exception guard) { failures.Add(guard); }
            finally { Attempt(Log.Dispose); }
            if (failures.Count != 0) throw new AggregateException("Guarded owned fixture cleanup failed.", failures);
        }
    }
}
