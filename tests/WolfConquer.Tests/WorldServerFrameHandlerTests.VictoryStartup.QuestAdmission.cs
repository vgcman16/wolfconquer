using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Theory]
    [InlineData("questCountExceedsImage")]
    [InlineData("truncatedFinalIntention")]
    public Task ActualD13VictoryStartupRejectsMalformedQuestBeforeAdmissionOrExpiryWrite(string damage) =>
        RunVictoryStartupAsync(quests: true, fixture => fixture.AssertMalformedQuestAdmissionAsync(damage));

    private sealed partial class VictoryStartupFixture
    {
        internal async Task AssertMalformedQuestAdmissionAsync(string damage)
        {
            Seed();
            File.AppendAllText(CharacterPath,
                "[PaidNobility]\r\nIdentifier=1293600\r\nLastNobilityDonation=100000000\r\n" +
                "PaidRank=9\r\nPeriodTime=0\r\nIsActive=True\r\nRank=King\r\nPosition=1\r\n",
                new UTF8Encoding(false, true));
            // Parse only: invoking the complete loader here would legitimately
            // restore the expired rank before the malformed quest is injected.
            var seededCharacter = CharacterSnapshotParser.ParseText(File.ReadAllText(CharacterPath), CharacterPath);
            Assert.Equal(Uid, seededCharacter.Uid);
            Assert.Equal("True", seededCharacter.PaidNobility["IsActive"]);
            Assert.Equal("0", seededCharacter.PaidNobility["PeriodTime"]); // DateTime.MinValue: expired.

            var malformed = questSeed.ToArray();
            if (damage == "questCountExceedsImage")
            {
                // Impossible for this real small image, but bounded even if a
                // future regression allocates before walking the structure.
                BinaryPrimitives.WriteInt32LittleEndian(malformed, 1024);
            }
            else
            {
                Assert.Equal("truncatedFinalIntention", damage);
                malformed = malformed[..^1]; // Keep both headers and the complete unrelated quest.
            }
            File.WriteAllBytes(QuestPath, malformed);
            var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
            Assert.Equal(3, files.Count);
            Assert.Equal(itemSeed, files[ItemPath]);
            Assert.Equal(malformed, files[QuestPath]);

            await RequestSeededAdmissionAsync();

            AssertFileSet(files);
            foreach (var (path, bytes) in files)
                Assert.Equal(bytes, File.ReadAllBytes(path));
            Assert.False(sessions.TryGet(connection, out _));
            // One exact structured error also excludes login success, NEW_ROLE,
            // startup item/quest frames, and any partial success sequence.
            var response = Assert.Single(sent);
            Assert.Equal(WorldSnapshotResponsePacket.Build(WorldSnapshotWireStatus.SnapshotError, Uid), response);
            Assert.True(PacketReader.TryRead(response, out var reader));
            Assert.Equal(WorldSnapshotWireStatus.SnapshotError, WorldSnapshotResponsePacket.ReadStatus(reader));
            Assert.Equal(Uid, WorldSnapshotResponsePacket.ReadAccountId(reader));
            Assert.Equal(0L, WorldSnapshotResponsePacket.ReadUid(reader));
            Assert.True(recovery.IsReadyFor(Users, Items, Quests));
            Assert.False(recovery.IsBlocked); // This reached snapshot loading, not a missing recovery owner.
            var diagnostic = log.ToString();
            Assert.Contains("WorldServer snapshot error accountId=1293600", diagnostic);
            Assert.Contains("reason=questSnapshotParseFailed status=SnapshotParseFailed", diagnostic);
            Assert.DoesNotContain("snapshot Success", diagnostic);
            Assert.DoesNotContain("NEW_ROLE", diagnostic);
        }
    }
}
