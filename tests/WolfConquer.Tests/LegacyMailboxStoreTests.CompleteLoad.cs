using System.Buffers.Binary;

namespace WolfConquer.Tests;

public sealed partial class LegacyMailboxStoreTests
{
    [Theory]
    [InlineData("negative-count")]
    [InlineData("trailing-byte")]
    [InlineData("duplicate-mail")]
    [InlineData("duplicate-attachment")]
    public void InvalidCompleteMailboxImageCannotReplacePreviouslyLoadedCache(string corruption)
    {
        const uint uid = 8_031;
        var store = CreateStore();
        store.Add(uid, CreateEntry(1, itemId: 11));
        store.Add(uid, CreateEntry(2, itemId: 12));
        store.Save(uid);
        var previous = store.Load(uid).ToArray();
        Assert.Equal(2, previous.Length);
        var path = store.GetMailboxPath(uid);
        var malformed = File.ReadAllBytes(path);
        Assert.Equal(4 + 2 * 740, malformed.Length);
        switch (corruption)
        {
            case "negative-count": BinaryPrimitives.WriteInt32LittleEndian(malformed, -1); break;
            case "trailing-byte": malformed = [.. malformed, 0x7f]; break;
            case "duplicate-mail": BinaryPrimitives.WriteUInt32LittleEndian(malformed.AsSpan(4 + 740 + 704), 1); break;
            case "duplicate-attachment": BinaryPrimitives.WriteUInt32LittleEndian(malformed.AsSpan(4 + 740 + 720), 11); break;
            default: throw new ArgumentOutOfRangeException(nameof(corruption));
        }
        File.WriteAllBytes(path, malformed);

        Assert.Throws<InvalidDataException>(() => store.Load(uid));

        Assert.Equal(malformed, File.ReadAllBytes(path));
        var retained = store.List(uid);
        Assert.Equal(previous.Length, retained.Count);
        for (var index = 0; index < previous.Length; index++) Assert.Same(previous[index], retained[index]);
    }
}
