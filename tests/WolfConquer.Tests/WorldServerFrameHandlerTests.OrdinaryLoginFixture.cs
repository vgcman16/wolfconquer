using System.Globalization;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    // Complete synthetic input routes + genuine leased registry. This fixture
    // supplies metadata and actual producer ownership, never Store acceptance,
    // saved-session injection, a replacement saver or a native send queue.
    private sealed class OrdinaryLoginFixture : IDisposable
    {
        private const uint Floor = 2_000_000;
        private const long Account = 1_293_600;
        private readonly CharacterSnapshotLoader loader;
        private readonly LegacyOrdinaryAdmissionRoots roots;
        private readonly LegacyItemUidRegistry registry;
        private readonly LegacyOrdinaryAdmissionUidOwner owner;
        private readonly string statePath;
        private readonly byte[] initialUidState;
        private readonly Dictionary<string, byte[]?> initialFiles;
        private readonly CharacterSnapshot initialCharacter;
        private int commitObservations;
        internal WorldDataCatalog WorldData { get; }

        internal OrdinaryLoginFixture(CharacterSnapshotLoader loader, WorldDataCatalog? worldData = null)
        {
            this.loader = loader;
            roots = LegacyOrdinaryAdmissionRoots.FromLoader(loader);
            var parent = Path.GetDirectoryName(roots.Users)!;
            var prefix = "wolf-world-handler-";
            var name = Path.GetFileName(parent);
            if (!Path.GetFileName(roots.Users).Equals("Users", StringComparison.Ordinal) ||
                !string.Equals(Path.GetDirectoryName(parent), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())),
                    StringComparison.OrdinalIgnoreCase) || !name.StartsWith(prefix, StringComparison.Ordinal) ||
                !Guid.TryParse(name[prefix.Length..], out _))
                throw new InvalidOperationException("Ordinary login fixture requires its actual GUID-owned Temp root.");
            foreach (var kind in roots.Kinds) Directory.CreateDirectory(roots.Root(kind)!);
            initialFiles = roots.Kinds.ToDictionary(kind => roots.PathFor(kind, Account), kind =>
                File.Exists(roots.PathFor(kind, Account)) ? File.ReadAllBytes(roots.PathFor(kind, Account)) : null);
            initialCharacter = loader.TryLoadByAccountIdForAdmission(Account).Snapshot
                ?? throw new InvalidOperationException("A real saved character is required by this ordinary fixture.");
            var mailbox = Path.Combine(parent, "PlayersMailList");
            Directory.CreateDirectory(mailbox);
            var options = new LegacyItemUidRegistryOptions(roots.Users, roots.Items!, mailbox,
                Path.Combine(parent, "RedeemContainer.bin"), Path.Combine(parent, "ClaimContainer.bin"), Floor,
                "owned synthetic fixture: quiescent historical floor");
            if (!LegacyItemUidRegistry.TryBootstrap(options, out var actual, out var reason) || actual is null)
                throw new InvalidOperationException(reason);
            registry = actual;
            try
            {
                statePath = Path.Combine(roots.Items!, ".managed-item-uids", "state.json");
                initialUidState = File.ReadAllBytes(statePath);
                var domain = Guid.ParseExact(JsonNode.Parse(initialUidState)!["Domain"]!.GetValue<string>(), "N");
                if (!LegacyOrdinaryAdmissionUidOwner.TryCreate(registry, domain, roots, mailbox, out var issuer, out reason) || issuer is null)
                    throw new InvalidOperationException(reason);
                owner = issuer;
                WorldData = worldData ?? Catalog();
            }
            catch { registry.Dispose(); throw; }
        }

        internal WorldServerFrameHandler CreateHandler(TextWriter log) =>
            new(loader, null, WorldData, owner,
                new OrdinaryAdmissionCommitObserver(log, ObserveCommittedInitialFiles),
                ConquerProtocolProfile.VisibleDx8D13);

        private void ObserveCommittedInitialFiles()
        {
            Interlocked.Increment(ref commitObservations);
            AssertInitialFileState(initialCharacter.MinHitPoints, runtimeSave: false);
        }

        internal void AssertCommitObserved() => Assert.Equal(1, Volatile.Read(ref commitObservations));

        internal static void PrepareLowLevelBaseline(string usersRoot)
        {
            Assert.True(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(Path.Combine(usersRoot, Account.ToString(CultureInfo.InvariantCulture) + ".ini"),
                Account, new Dictionary<string, string> { ["Vitaliti"] = "3", ["Spirit"] = "0", ["Atributes"] = "0",
                    ["MinMana"] = "0", ["MaxMana"] = "0" }, detailedIoFailures: true, out var reason), reason);
        }

        internal async Task AssertCommittedAndRepeatedAsync(WorldServerFrameHandler handler,
            ConquerTcpConnection connection, List<byte[]> sent, int newItems = 0,
            long? expectedRuntimeHitpoints = null)
        {
            AssertCommitObserved();
            var session = GetSession(handler, connection);
            Assert.Equal(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes(), sent[0]);
            Assert.False(LegacyTradeJournal.HasPending(roots.Users));
            Assert.True(registry.TryReadActiveProducerIntents(owner.Domain, out var active, out var reason), reason);
            Assert.Empty(active);
            Assert.Equal(0, newItems); // Ordinary saved login has no birth/migration allocator.
            Assert.Equal(initialUidState, File.ReadAllBytes(statePath));
            Assert.True(registry.TryReadProducerIntents(owner.Domain, out var history, out reason), reason);
            Assert.Empty(history);
            AssertPreservedInitialState(session, expectedRuntimeHitpoints);
            var before = roots.Kinds.ToDictionary(kind => roots.PathFor(kind, Account), kind =>
                File.Exists(roots.PathFor(kind, Account)) ? File.ReadAllBytes(roots.PathFor(kind, Account)) : null);
            var uidState = File.ReadAllBytes(statePath);
            var packetCount = sent.Count;
            await handler.HandleFrameAsync(connection, ReadFrame(new LegacyMsgLoginGamePacket((uint)Account).ToFrameBytes()));
            AssertCommitObserved();
            Assert.Same(session, GetSession(handler, connection));
            Assert.Equal(packetCount, sent.Count);
            Assert.Equal(uidState, File.ReadAllBytes(statePath));
            foreach (var (path, bytes) in before)
                if (bytes is null) Assert.False(File.Exists(path)); else Assert.Equal(bytes, File.ReadAllBytes(path));
        }

        private void AssertPreservedInitialState(WorldSession session, long? expectedRuntimeHitpoints)
        {
            var hitpoints = expectedRuntimeHitpoints ?? initialCharacter.MinHitPoints;
            AssertInitialFileState(hitpoints, runtimeSave: hitpoints != initialCharacter.MinHitPoints);
            var empty = new Dictionary<string, string>();
            Assert.Equal(initialCharacter with { Online = 1, MinHitPoints = hitpoints, SourceFile = "", Attributes = empty,
                    PaidNobility = empty, PlayerQuests = PlayerQuestSnapshot.Empty },
                session.Character with { SourceFile = "", Attributes = empty, PaidNobility = empty, PlayerQuests = PlayerQuestSnapshot.Empty });
        }

        private void AssertInitialFileState(long hitpoints, bool runtimeSave)
        {
            var persisted = loader.TryLoadByAccountIdForAdmission(Account).Snapshot;
            Assert.NotNull(persisted);
            var empty = new Dictionary<string, string>();
            Assert.Equal(initialCharacter with { Online = 1, MinHitPoints = hitpoints, SourceFile = "", Attributes = empty,
                    PaidNobility = empty, PlayerQuests = PlayerQuestSnapshot.Empty },
                persisted with { SourceFile = "", Attributes = empty, PaidNobility = empty, PlayerQuests = PlayerQuestSnapshot.Empty });
            foreach (var kind in roots.Kinds)
            {
                var path = roots.PathFor(kind, Account);
                var before = initialFiles[path];
                if (kind == LegacyTradeJournalFileKind.Character)
                {
                    Assert.NotNull(before);
                    var text = CharacterSnapshotFileEncoding.Decode(before, out var encoding);
                    Assert.True(initialCharacter.Online is 0 or 7);
                    var expected = System.Text.RegularExpressions.Regex.Replace(text, "(?m)^Online=(0|7)(?=\\r?$)", "Online=1");
                    var committed = encoding.GetPreamble().Concat(encoding.GetBytes(expected)).ToArray();
                    var final = runtimeSave ? OrdinaryAdmissionImageExpectations.RuntimeHpSave(committed, hitpoints,
                        initialCharacter.MaxMana, initialCharacter.ExpBallUsed) : committed;
                    Assert.Equal(final, File.ReadAllBytes(path));
                }
                else if (kind == LegacyTradeJournalFileKind.Spells) AssertSavedSpellBytes(path, before);
                else if (kind == LegacyTradeJournalFileKind.Proficiencies && before is null)
                    Assert.Equal(new byte[4], File.ReadAllBytes(path));
                else if (before is null) Assert.False(File.Exists(path));
                else Assert.Equal(before, File.ReadAllBytes(path));
            }
        }

        private static void AssertSavedSpellBytes(string path, byte[]? before)
        {
            if (before is null && !File.Exists(path)) return;
            var after = File.ReadAllBytes(path);
            if (before is not null && before.AsSpan().SequenceEqual(after)) return;
            var original = before ?? new byte[4];
            var count = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(original);
            Assert.Equal(original.Length + 12, after.Length);
            Assert.Equal(count + 1, System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(after));
            Assert.Equal(original.AsSpan(4).ToArray(), after.AsSpan(4, original.Length - 4).ToArray());
            Assert.Equal(new byte[] { 0x56, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, after.AsSpan(original.Length).ToArray());
        }

        internal static WorldDataCatalog Catalog(IReadOnlyList<Npc>? npcs = null)
        {
            var items = new[] { 132009, 420301, 150003 }.Select(id =>
            {
                var fields = Enumerable.Repeat("0", 60).ToArray();
                fields[0] = id.ToString(CultureInfo.InvariantCulture); fields[1] = "OwnedStarter" + id;
                fields[ItemType.DurabilityFieldIndex] = "1"; // Explicit valid metadata, no runtime fallback.
                return new ItemType(id, fields[1], fields);
            }).ToArray();
            var stats = new List<ClassLevelStat>
            {
                Stat("Trojan", 1, 5, 2, 3, 0), Stat("Trojan", 4, 6, 3, 4, 0),
                Stat("Trojan", 31, 50, 20, 30, 0), Stat("Trojan", 109, 100, 40, 60, 0),
            };
            foreach (var family in new[] { "Trojan", "Warrior", "Archer", "Ninja", "Monk", "Pirate", "LongLee", "Taoist", "WindWalker" })
                stats.Add(Stat(family, 110, 100, 40, 60, 0));
            // Literal cast gate rejects Fire1000 for nonreborn Trojan130.
            // Ordinary admission preserves that saved learned record; this
            // metadata is no authority to purge it or reset saved stats.
            var operation = new MagicTypeOperationEntry(1, 1, 14, 0, 0, [1000], [1000],
                "owned-magic-operation", 1, "1 1 14 0 0 1000", ["1", "1", "14", "0", "0", "1000"]);
            return LegacyItemWorldDataFixture.Create(items, [], npcs: npcs, classLevelStats: stats, magicTypeOperations: [operation]);
        }

        private static ClassLevelStat Stat(string family, int level, int strength, int agility, int vitality, int spirit) =>
            new(family, level, strength, agility, vitality, spirit, "owned-class-stats", level,
                level.ToString(CultureInfo.InvariantCulture), $"{strength},{agility},{vitality},{spirit}", "owned synthetic row",
                [strength.ToString(CultureInfo.InvariantCulture), agility.ToString(CultureInfo.InvariantCulture),
                    vitality.ToString(CultureInfo.InvariantCulture), spirit.ToString(CultureInfo.InvariantCulture)]);

        public void Dispose() => registry.Dispose(); // Enclosing GUID test fixture owns directory cleanup.
    }
}
