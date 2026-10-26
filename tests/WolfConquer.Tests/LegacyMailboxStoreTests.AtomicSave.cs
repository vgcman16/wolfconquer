using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMailboxStoreTests
{
    [Theory]
    [InlineData("title")]
    [InlineData("sender")]
    [InlineData("content")]
    public void SaveTextSerializationFailureRetainsPreviousCompleteFileAndCanRetry(string field)
    {
        const uint uid = 8_101;
        var store = CreateStore();
        store.Add(uid, CreateEntry(1, itemId: 0));
        store.Save(uid);
        var files = CaptureAtomicSaveFiles();
        var invalid = CreateEntry(1, title: field == "title" ? null! : "Title",
            senderName: field == "sender" ? null! : "Sender", content: field == "content" ? null! : "Content", itemId: 0);
        store.Add(uid, invalid); // Caller mutation is deliberate; Save does not claim to roll it back.

        Assert.ThrowsAny<ArgumentException>(() => store.Save(uid));

        AssertAtomicSaveFiles(files);
        Assert.Same(invalid, Assert.Single(store.List(uid)));
        var replacement = CreateEntry(1, title: "Retry succeeded", itemId: 0);
        store.Add(uid, replacement);
        store.Save(uid);
        Assert.Equal(replacement.Title, Assert.Single(CreateStore().Load(uid)).Title);
    }

    [Fact]
    public void SaveEncoderFailureInLaterRowCannotReplacePreviousCompleteFile()
    {
        const uint uid = 8_102;
        var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        var store = new LegacyMailboxStore(rootDirectory, strictUtf8, () => now);
        store.Add(uid, CreateEntry(1, itemId: 0));
        store.Save(uid);
        var files = CaptureAtomicSaveFiles();
        store.Add(uid, CreateEntry(2, content: "Invalid surrogate: \ud800", itemId: 0));

        Assert.Throws<EncoderFallbackException>(() => store.Save(uid));

        AssertAtomicSaveFiles(files);
        Assert.Equal(2, store.List(uid).Count);
        store.Add(uid, CreateEntry(2, content: "Corrected text", itemId: 0));
        store.Save(uid);
        var loaded = new LegacyMailboxStore(rootDirectory, strictUtf8, () => now).Load(uid);
        Assert.Equal(new uint[] { 1, 2 }, loaded.Select(row => row.Id));
        Assert.Equal("Corrected text", loaded[1].Content);
    }

    [Fact]
    public void SaveDuplicateAttachmentRefusesBeforeReplacingAValidImage()
    {
        const uint uid = 8_103;
        var store = CreateStore();
        store.Add(uid, CreateEntry(1, itemId: 901));
        store.Save(uid);
        var files = CaptureAtomicSaveFiles();
        store.Add(uid, CreateEntry(2, itemId: 901));

        Assert.Throws<InvalidDataException>(() => store.Save(uid));

        AssertAtomicSaveFiles(files);
        Assert.Equal(2, store.List(uid).Count);
        Assert.Equal(1U, Assert.Single(CreateStore().Load(uid)).Id);
        store.Add(uid, CreateEntry(2, itemId: 902));
        store.Save(uid);
        Assert.Equal(new uint[] { 901, 902 }, CreateStore().Load(uid).Select(row => row.ItemId));
    }

    [Fact]
    public void SaveAfterLastEmptyRowRemovalWritesCompleteCountZeroImage()
    {
        const uint uid = 8_104;
        var store = CreateStore();
        store.Add(uid, CreateEntry(1, money: 0, eMoney: 0, itemId: 0, flag: 0));
        store.Save(uid);
        Assert.True(store.TryRemoveIfEmpty(uid, 1, out _));

        store.Save(uid);

        Assert.Equal(new byte[4], File.ReadAllBytes(store.GetMailboxPath(uid)));
        Assert.Empty(CreateStore().Load(uid));
        Assert.Single(CaptureAtomicSaveFiles());
    }

    [Fact]
    public void SavePreservesConfiguredEncodingAndEstablishedNumericOffsets()
    {
        const uint uid = 8_105;
        var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        var store = new LegacyMailboxStore(rootDirectory, strictUtf8, () => now);
        var mail = CreateEntry(71, title: "Wolf狼", senderName: "系统", content: "Reward ✓",
            money: 0x0102030405060708UL, eMoney: 0x11223344U, itemId: 0x55667788U,
            emoneyRecordType: 0x10203040U, flag: 0x50607080U);
        store.Add(uid, mail);

        store.Save(uid);

        var bytes = File.ReadAllBytes(store.GetMailboxPath(uid));
        Assert.Equal(744, bytes.Length);
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(bytes));
        Assert.Equal(new byte[] { 0x57, 0x6f, 0x6c, 0x66, 0xe7, 0x8b, 0xbc }, bytes.AsSpan(4, 7).ToArray());
        Assert.Equal(new byte[] { 0xe7, 0xb3, 0xbb, 0xe7, 0xbb, 0x9f }, bytes.AsSpan(68, 6).ToArray());
        Assert.Equal(71U, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(708)));
        Assert.Equal(0x0102030405060708UL, BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(712)));
        Assert.Equal(0x11223344U, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(720)));
        Assert.Equal(0x55667788U, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(724)));
        Assert.Equal(0x10203040U, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(728)));
        Assert.Equal(0x50607080U, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(732)));
        Assert.Equal(mail.Timer, BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(736)));
        var loaded = Assert.Single(new LegacyMailboxStore(rootDirectory, strictUtf8, () => now).Load(uid));
        Assert.Equal(mail.Title, loaded.Title);
        Assert.Equal(mail.SenderName, loaded.SenderName);
        Assert.Equal(mail.Content, loaded.Content);
        Assert.Single(CaptureAtomicSaveFiles());
    }

    private Dictionary<string, byte[]> CaptureAtomicSaveFiles() => Directory.GetFiles(rootDirectory, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(rootDirectory, path), File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);

    private void AssertAtomicSaveFiles(IReadOnlyDictionary<string, byte[]> expected)
    {
        var actual = CaptureAtomicSaveFiles();
        Assert.Equal(expected.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase),
            actual.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase));
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, actual[path]);
    }
}
