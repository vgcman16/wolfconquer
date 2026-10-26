using System.IO.Compression;
using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed class ClientConfigRequestedGzipDifferentialTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(31)]
    [InlineData(258)]
    [InlineData(16383)]
    [InlineData(16384)]
    [InlineData(16385)]
    [InlineData(32767)]
    [InlineData(32768)]
    [InlineData(32769)]
    [InlineData(65535)]
    [InlineData(65536)]
    [InlineData(131073)]
    public void StandardWriterMembersPreserveEveryByteAcrossNativeBufferAndHistoryBoundaries(int length)
    {
        CompressionLevel[] levels = [CompressionLevel.NoCompression, CompressionLevel.Fastest,
            CompressionLevel.Optimal, CompressionLevel.SmallestSize];
        foreach (var level in levels)
        {
            foreach (var payload in Payloads(length))
            {
                using var encoded = new MemoryStream();
                using (var writer = new GZipStream(encoded, level, leaveOpen: true)) writer.Write(payload);
                var input = encoded.ToArray();
                var original = input.ToArray();

                var decoded = ClientConfigRequestedGzip.Decode(input, payload.Length);

                Assert.Equal(payload, decoded);
                Assert.Equal(original, input);
            }
        }
    }

    [Fact]
    public void ValidEmptyMemberIsDistinctFromAStandardWriterThatNeverReceivesBytes()
    {
        // Independently specified gzip header, final empty fixed block, CRC0 and ISIZE0.
        var member = Convert.FromHexString("1F8B080000000000000303000000000000000000");
        Assert.Empty(ClientConfigRequestedGzip.Decode(member, 0));
        CompressionLevel[] levels = [CompressionLevel.NoCompression, CompressionLevel.Fastest,
            CompressionLevel.Optimal, CompressionLevel.SmallestSize];
        foreach (var level in levels)
        {
            using var encoded = new MemoryStream();
            using (var writer = new GZipStream(encoded, level, leaveOpen: true)) writer.Write([]);
            var absentMember = encoded.ToArray();
            Assert.Empty(absentMember);
            Assert.Throws<InvalidDataException>(() => ClientConfigRequestedGzip.Decode(absentMember, 0));
        }
    }

    private static IEnumerable<byte[]> Payloads(int length)
    {
        yield return new byte[length];
        var patterned = new byte[length];
        for (var index = 0; index < length; index++) patterned[index] = unchecked((byte)(index * 73 + index / 128));
        yield return patterned;
        var random = new byte[length];
        new Random(20261026).NextBytes(random);
        yield return random;
    }
}
