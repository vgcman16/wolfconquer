using System.Reflection;
using System.Text;
using System.Text.Json;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// DATA aggregate-bound coverage only. The strings have valid JSON/base64
// shapes, not valid Character/Items/Profs recipes. No live journal, file,
// participant, persistence receipt or recovery owner is manufactured here.
public sealed class LegacyPhysicalHitProgressionEnvelopeTests
{
    private delegate void EnvelopePreflight(ReadOnlySpan<byte> bytes);
    private static readonly EnvelopePreflight Preflight = typeof(LegacyTradeJournal)
        .GetMethod("PreflightHitProgressionEnvelope", BindingFlags.NonPublic | BindingFlags.Static)!
        .CreateDelegate<EnvelopePreflight>();

    private const long JournalLimit = 192L * 1024 * 1024;
    private const int EncodedTargetLimit = ((16 * 1024 * 1024 + 2) / 3) * 4;
    // Independent token inventory for EnvelopeShape below: 61 tokens, including
    // 26 property names totaling 241 bytes; non-image string contents are 32 bytes
    // of transaction ID plus 65 encoded path bytes (14+14+15+22). Whitespace is
    // excluded. Each image character contributes 3; no production budget helper
    // or parser computes this expected boundary.
    private const long FixedBudget = 16_384L + 61L * 64 + 6L * (241 + 97);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FiveIndividuallyBoundedImagesCrossActualOuterBudgetByOneBase64Quartet(bool versionLast)
    {
        Assert.Equal(22_316L, FixedBudget);
        int acceptedCharacters = checked((int)((JournalLimit - FixedBudget) / 12 * 4));
        Assert.Equal(67_101_424, acceptedCharacters);
        Assert.Equal(JournalLimit - 4, FixedBudget + 3L * acceptedCharacters);
        Assert.Equal(JournalLimit + 8, FixedBudget + 3L * (acceptedCharacters + 4));

        // One bounded buffer, reused in place. Tests in this class execute
        // sequentially; there is no DOM, large string or payload-sized clone.
        var envelope = BuildEnvelope(acceptedCharacters + 4, versionLast);
        Assert.InRange(envelope.Bytes.Length, 1, 64 * 1024 * 1024);
        Assert.Equal(acceptedCharacters + 4, envelope.ImageLengths.Sum());
        Assert.All(envelope.ImageLengths, length =>
        {
            Assert.Equal(0, length % 4);
            Assert.InRange(length, 4, EncodedTargetLimit);
            Assert.True(FixedBudget + 3L * length < JournalLimit);
        });

        Preflight("{}"u8); // Warm the actual scanner/delegate outside measurement.
        long allocationStart = GC.GetAllocatedBytesForCurrentThread();
        var error = Assert.Throws<JsonException>(() => Preflight(envelope.Bytes));
        long rejectedAllocations = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
        Assert.Equal("Hit progression aggregate envelope exceeds bound.", error.Message);
        // This observes the scanner itself, not the caller's allocation timing.
        // Any DOM/base64 materialization of this payload would exceed 1 MiB.
        Assert.InRange(rejectedAllocations, 0L, 1024L * 1024);

        // Remove four characters from the final image by moving only the small
        // footer. The exact same encoded images now fit the independent budget.
        envelope.Footer.AsSpan().CopyTo(envelope.Bytes.AsSpan(envelope.LastImageEnd - 4));
        int acceptedLength = envelope.Bytes.Length - 4;
        allocationStart = GC.GetAllocatedBytesForCurrentThread();
        Preflight(envelope.Bytes.AsSpan(0, acceptedLength));
        long acceptedAllocations = GC.GetAllocatedBytesForCurrentThread() - allocationStart;
        Assert.InRange(acceptedAllocations, 0L, 1024L * 1024);
    }

    private sealed record EnvelopeBuffer(byte[] Bytes, int[] ImageLengths, int LastImageEnd, byte[] Footer);

    private static EnvelopeBuffer BuildEnvelope(int imageCharacters, bool versionLast)
    {
        Assert.Equal(0, imageCharacters % 4);
        string shape = EnvelopeShape
            .Replace("$VERSION_FIRST$", versionLast ? "" : "\"Version\":24,", StringComparison.Ordinal)
            .Replace("$VERSION_LAST$", versionLast ? ",\"Version\":24" : "", StringComparison.Ordinal);
        var chunks = shape.Split("$IMAGE$", StringSplitOptions.None).Select(chunk => Encoding.UTF8.GetBytes(chunk)).ToArray();
        Assert.Equal(6, chunks.Length);
        int quartets = imageCharacters / 4;
        var lengths = Enumerable.Range(0, 5).Select(index =>
            checked((quartets / 5 + (index < quartets % 5 ? 1 : 0)) * 4)).ToArray();
        var buffer = new byte[checked(imageCharacters + chunks.Sum(chunk => chunk.Length))];
        int offset = 0;
        for (int index = 0; index < lengths.Length; index++)
        {
            chunks[index].AsSpan().CopyTo(buffer.AsSpan(offset)); offset += chunks[index].Length;
            buffer.AsSpan(offset, lengths[index]).Fill((byte)'A'); offset += lengths[index];
        }
        int lastImageEnd = offset;
        chunks[^1].AsSpan().CopyTo(buffer.AsSpan(offset)); offset += chunks[^1].Length;
        Assert.Equal(buffer.Length, offset);
        return new(buffer, lengths, lastImageEnd, chunks[^1]);
    }

    private const string EnvelopeShape = """
        {
          $VERSION_FIRST$
          "TransactionId":"00000000000000000000000000000001",
          "Changes":[
            {"Kind":0,"FileId":9002,"Before":"$IMAGE$","After":"$IMAGE$"},
            {"Kind":6,"FileId":101,"Before":"$IMAGE$","After":"$IMAGE$"}
          ],
          "HitProgressionDomain":{
            "Users":"C:\\j24\\Users","Items":"C:\\j24\\Items",
            "Spells":"C:\\j24\\Spells","Proficiencies":"C:\\j24\\Proficiencies"
          },
          "PhysicalHitProgression":{
            "ActorAccountId":9001,"ActorUid":101,"TargetAccountId":9002,"TargetUid":202,
            "RawExperience":123,"AppliedDamage":100,"ActorAlternate":false,
            "ActorItems":"$IMAGE$","Death":null
          }
          $VERSION_LAST$
        }
        """;
}
