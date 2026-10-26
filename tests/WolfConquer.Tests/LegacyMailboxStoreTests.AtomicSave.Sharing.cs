namespace WolfConquer.Tests;

public sealed partial class LegacyMailboxStoreTests
{
    [WindowsTradeJournalFailureFact]
    public void SaveReplacementSharingFailureRetainsCompleteFileAndRetriesAfterRealHandleRelease()
    {
        const uint uid = 8_106;
        var store = CreateStore();
        store.Add(uid, CreateEntry(1, itemId: 0));
        store.Save(uid);
        var files = CaptureAtomicSaveFiles();
        store.Add(uid, CreateEntry(2, itemId: 0));
        using (var held = new FileStream(store.GetMailboxPath(uid), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.ThrowsAny<IOException>(() => store.Save(uid));
            AssertAtomicSaveFiles(files);
            Assert.Equal(2, store.List(uid).Count);
        }

        store.Save(uid);

        Assert.Equal(new uint[] { 1, 2 }, CreateStore().Load(uid).Select(row => row.Id));
        Assert.Single(CaptureAtomicSaveFiles());
    }
}
