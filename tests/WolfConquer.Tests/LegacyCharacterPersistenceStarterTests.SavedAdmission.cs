using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyCharacterPersistenceStarterTests
{
    private static async Task AssertSavedAdmissionWithOwnerAcrossRelogAsync(
        LegacyOwnedAdmissionRuntimeFixture fixture,
        PlayerItemSnapshot items,
        PlayerSpellSnapshot spells,
        long expectedClass,
        long expectedLevel,
        long expectedMoney,
        long expectedConquerPoints,
        uint proficiencyId)
    {
        const uint uid = LegacyOwnedAdmissionRuntimeFixture.Uid;
        Assert.True(CharacterSnapshotFileCoordinator.TryUpdateCharacterAttributes(fixture.UserPath, uid,
            new Dictionary<string, string>
            {
                ["ConquerPoints"] = expectedConquerPoints.ToString(CultureInfo.InvariantCulture),
                ["SecurityPass"] = "0,0,0", ["SavedSentinel"] = "retain-existing-business-state",
            }, detailedIoFailures: true, out var reason), reason);
        new PlayerItemSnapshotLoader(fixture.Items).SaveByCharacterUid(uid, items);
        new PlayerSpellSnapshotLoader(fixture.Spells).SaveByCharacterUid(uid, spells);
        var proficiencies = new PlayerProficiencySnapshot(
            [new(proficiencyId, Level: 2, PreviousLevel: 1, Experience: 22, NeededExperience: 100)]);
        new PlayerProficiencySnapshotLoader(fixture.Proficiencies).SaveByCharacterUid(uid, proficiencies);
        var dataBeforeBootstrap = fixture.Files();
        var characterKey = Path.GetRelativePath(fixture.Root, fixture.UserPath);
        var originalText = CharacterSnapshotFileEncoding.Decode(dataBeforeBootstrap[characterKey], out var encoding);
        var original = CharacterSnapshotParser.ParseText(originalText);
        Assert.Equal((long)uid, original.Uid);
        Assert.Equal((expectedClass, expectedLevel, expectedMoney, expectedConquerPoints),
            (original.Class, original.Level, original.Money, original.ConquerPoints));
        Assert.Equal(0, original.Online);
        // Keep the retired heuristic's exact easy-to-misclassify financial,
        // lineage and zero-progress guards, rather than bypassing it with a
        // nonzero bank/reborn/XP balance. Appearance is not birth authority.
        Assert.Equal((0L, 0L, 0L, 0L, 0L, 0L),
            (original.Reborn, original.FirstClass, original.SecondClass, original.Experience,
                original.BoundConquerPoints, original.BankConquerPoints));
        Assert.Equal(0, original.PkPoints);
        Assert.Equal(0, original.AttributePoints);
        Assert.All(spells.Spells, spell =>
        {
            Assert.Equal(0, spell.Level);
            Assert.Equal(0, spell.Experience);
            Assert.Equal(0, spell.PreviousLevel);
            Assert.Equal(0, spell.SoulLevel);
            Assert.Equal(0, spell.UseSpellSoul);
        });

        // Actual quiescent catalog scan/bootstrap, followed by the real public
        // startup factory. No injected Store, accepting saver or fake UID owner.
        fixture.Bootstrap();
        var expectedFiles = fixture.Files();
        foreach (var (path, bytes) in dataBeforeBootstrap) Assert.Equal(bytes, expectedFiles[path]);
        Assert.Equal(1, Regex.Matches(originalText, "(?m)^Online=0(?=\\r?$)").Count);
        var admittedText = Regex.Replace(originalText, "(?m)^Online=0(?=\\r?$)", "Online=1");
        expectedFiles[characterKey] = encoding.GetPreamble().Concat(encoding.GetBytes(admittedText)).ToArray();
        var worldData = SavedAdmissionCatalog(items);

        // Two independently opened public lifetimes/handlers/connections prove
        // relog from disk, not just replay on an already admitted Store entry.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            LegacyOwnedAdmissionRuntime? runtime = null;
            ConquerTcpConnection? connection = null;
            var errors = new List<Exception>();
            using var log = new StringWriter();
            try
            {
                Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(fixture.Loader, fixture.Options(), out var opened, out reason), reason);
                runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
                var handler = new WorldServerFrameHandler(fixture.Loader, null, worldData,
                    LegacyRuntimeUpdateLoopOptions.Disabled, log,
                    protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: runtime);
                var sent = new List<byte[]>();
                connection = CreateSendingConnection(sent);
                await handler.HandleFrameAsync(connection, ReadFrame(new LegacyMsgLoginGamePacket(uid).ToFrameBytes()));

                Assert.NotEmpty(sent);
                Assert.Equal(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes(), sent[0]);
                var admitted = GetSession(handler, connection);
                AssertSavedAdmissionHero(sent, original);
                AssertSavedAdmissionScalars(original, admitted.Character);
                Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(items), PlayerItemSnapshotLoader.ToLegacyBytes(admitted.PlayerItems));
                Assert.Equal(spells.Spells, admitted.PlayerSpells.Spells);
                Assert.Equal(proficiencies.Proficiencies, admitted.PlayerProficiencies.Proficiencies);
                fixture.AssertFiles(expectedFiles); // Full images + all UID history, not selected rows.
                Assert.False(LegacyTradeJournal.HasPending(fixture.Users));
                Assert.Single(log.ToString().Split('\n').Where(line =>
                    line.Contains("ordinary admission committed key=", StringComparison.Ordinal)));

                var sentCount = sent.Count;
                await handler.HandleFrameAsync(connection, ReadFrame(new LegacyMsgLoginGamePacket(uid).ToFrameBytes()));
                Assert.Same(admitted, GetSession(handler, connection));
                Assert.Equal(sentCount, sent.Count);
                fixture.AssertFiles(expectedFiles);
            }
            catch (Exception error) { errors.Add(error); }
            // Both owned cleanup steps are attempted and retain distinct errors.
            // Actual disconnect work is drained before reopening the UID lease.
            if (connection is not null)
            {
                try { await connection.CompleteTransportCloseAsync(); }
                catch (Exception error) { errors.Add(error); }
            }
            if (runtime is not null)
            {
                try { await runtime.DisposeAsync(); }
                catch (Exception error) { if (!errors.Any(value => ReferenceEquals(value, error))) errors.Add(error); }
            }
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
            fixture.AssertFiles(expectedFiles);
        }
        // A failed assertion or uncertain close/drain retains the genuine
        // fixture. Delete only after both complete lifetimes passed and drained.
        fixture.Dispose();
    }

    private static WorldDataCatalog SavedAdmissionCatalog(PlayerItemSnapshot items)
    {
        var metadata = items.InventoryItems.Concat(items.EquipmentItems).Select(item => item.ItemId).Distinct().Select(id =>
        {
            var fields = Enumerable.Repeat("0", 60).ToArray();
            fields[0] = id.ToString(CultureInfo.InvariantCulture);
            fields[1] = "OwnedSavedItem" + fields[0];
            fields[ItemType.DurabilityFieldIndex] = "1";
            return new ItemType(checked((int)id), fields[1], fields);
        }).ToArray();
        return LegacyItemWorldDataFixture.Create(metadata, []);
    }

    private static void AssertSavedAdmissionScalars(CharacterSnapshot before, CharacterSnapshot admitted)
    {
        var empty = new Dictionary<string, string>();
        // Runtime-only attribute projection is distinct from saved images;
        // complete file bytes above independently cover every stored attribute.
        Assert.Equal(before with { Online = 1, SourceFile = "", Attributes = empty, PaidNobility = empty,
                PlayerQuests = PlayerQuestSnapshot.Empty },
            admitted with { SourceFile = "", Attributes = empty, PaidNobility = empty,
                PlayerQuests = PlayerQuestSnapshot.Empty });
        Assert.Equal((before.BoundConquerPoints, before.BankConquerPoints),
            (admitted.BoundConquerPoints, admitted.BankConquerPoints));
    }

    private static void AssertSavedAdmissionHero(IReadOnlyList<byte[]> sent, CharacterSnapshot expected)
    {
        var loaderFrame = Assert.Single(sent.Select(ReadFrame).Where(frame => frame.Header.Opcode == LegacyLoaderHeroInfoPacket.Opcode));
        Assert.True(LegacyLoaderHeroInfoPacket.TryParse(loaderFrame, out var loaderHero));
        Assert.Equal((checked((uint)expected.Uid), checked((uint)expected.Class), checked((byte)expected.Level), expected.Name),
            (loaderHero.Uid, loaderHero.Class, loaderHero.Level, loaderHero.Name));
        var protoFrame = Assert.Single(sent.Select(ReadFrame).Where(frame => frame.Header.Opcode == LegacyProtoHeroInfoPacket.Opcode));
        Assert.True(LegacyProtoHeroInfoPacket.TryParse(protoFrame, out var protoHero));
        Assert.Equal(checked((ulong)expected.Uid), protoHero.GetNumericField(1));
        Assert.Equal(checked((ulong)expected.Money), protoHero.GetNumericField(5));
        Assert.Equal(checked((ulong)expected.ConquerPoints), protoHero.GetNumericField(6));
        Assert.Equal(checked((ulong)expected.Experience), protoHero.GetNumericField(7));
        Assert.Equal(checked((ulong)expected.Strength), protoHero.GetNumericField(13));
        Assert.Equal(checked((ulong)expected.Agility), protoHero.GetNumericField(14));
        Assert.Equal(checked((ulong)expected.Vitality), protoHero.GetNumericField(15));
        Assert.Equal(checked((ulong)expected.MinHitPoints), protoHero.GetNumericField(18));
        Assert.Equal(checked((ulong)expected.Level), protoHero.GetNumericField(21));
        Assert.Equal(checked((ulong)expected.Class), protoHero.GetNumericField(22));
        Assert.Equal(checked((ulong)expected.BoundConquerPoints), protoHero.GetNumericField(36));
    }
}
