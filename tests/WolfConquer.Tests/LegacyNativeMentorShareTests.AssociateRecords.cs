using WolfConquer.Core.World;
using Associate = WolfConquer.Core.World.LegacyNativeAssociateRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMentorShareTests
{
    private static string Record(uint owner = 17, byte mode = 5, uint uid = 21, uint credit = 10,
        string online = "1", string bp = "237") => string.Join("/", new object[]
    {
        owner, mode, credit, 20, 30, uid, 123456789012345ul, 41, 42, 43, "Saved", 44,
        online, bp, 1002, 1003, 129, 12, "SavedSig", 1005, ""
    });

    [Fact]
    public Task MemberAndOwnerRecordEnumerationReadCurrentLiveBpAndDeferredOwnerCreditsWithoutSnapshotProjection() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var peer = scope.Peer(21); var owner = scope.Player.Associate!;
        var member = new Associate.Member { UID = 21, Timer = 77, ExpBalls = 41, Stone = 42, Blessing = 43, Name = "Saved", KillsCount = 44,
            KillMap = 1002, Mesh = 1003, Level = 129, NobilityRank = 12, Signature = "SavedSig", Class = 1005 };
        owner.Add(5, member); var deferred = owner.ToStringMember();
        owner.Mentor_ExpBalls = 10; owner.Mentor_Blessing = 20; owner.Mentor_Stones = 30; peer.Equipment!.BattlePower = 200;
        string[] values = Assert.Single(deferred).Split('/');
        Assert.Equal(new[] { "17", "5", "10", "20", "30", "21", "77", "41", "42", "43", "Saved", "44", "1", "337", "1002", "1003", "129", "12", "SavedSig", "1005", "" }, values);
        LegacyNativePoolClients.GamePoll!.TryRemove(21, out _);
        string[] offline = member.ToString().Split('/'); Assert.Equal("0", offline[7]); Assert.Equal("0", offline[8]); Assert.Equal("129", offline[11]);
    });

    [Fact]
    public Task WholeAssociateSaveAndLoadRoundTripOwnedFileWithActualCountHeaderAndEverySavedField() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new Files(); var peer = scope.Peer(21); var owner = scope.Player.Associate!;
        owner.Mentor_ExpBalls = 10; owner.Mentor_Blessing = 20; owner.Mentor_Stones = 30;
        owner.Add(5, new Associate.Member { UID = 21, Timer = 123456789012345, ExpBalls = 41, Stone = 42, Blessing = 43,
            Name = "Saved", KillsCount = 44, KillMap = 1002, Mesh = 1003, Level = 129, NobilityRank = 12, Signature = "SavedSig", Class = 1005 });
        Associate.Save(); string path = Path.Combine(files.Root, "Associate.txt");
        Assert.Equal(new[] { "Count=1", Record() }, File.ReadAllLines(path));
        Associate.Associates = new(); Associate.Load();
        var restored = Associate.Associates[17]; var row = restored.Associat[5][21];
        Assert.Equal(17u, restored.MyUID); Assert.Equal(10u, restored.Mentor_ExpBalls); Assert.Equal(20u, restored.Mentor_Blessing); Assert.Equal(30u, restored.Mentor_Stones);
        Assert.Null(restored.MyClient); Assert.Empty(restored.OnlineApprentice); Assert.Equal(0u, restored.Online);
        Assert.Equal(123456789012345ul, row.Timer); Assert.Equal(41u, row.ExpBalls); Assert.Equal(42u, row.Stone); Assert.Equal(43u, row.Blessing);
        Assert.Equal("Saved", row.Name); Assert.Equal((ushort)44, row.KillsCount); Assert.Equal(1002u, row.KillMap); Assert.Equal(1003u, row.Mesh);
        Assert.Equal(129u, row.Level); Assert.Equal(12u, row.NobilityRank); Assert.Equal("SavedSig", row.Signature); Assert.Equal(1005u, row.Class);
        Assert.Equal(1u, row.IsOnline); Assert.Equal((ushort)237, row.BattlePower); Assert.Same(peer, LegacyNativePoolClients.GamePoll![21]);
        Assert.Same(owner, scope.Player.Associate); //Load does not rebind a live player to the new dictionary owner.
        using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    });

    [Fact]
    public Task AssociateLoadIgnoresSavedOnlineAndBpColumnsAndKeepsExistingCreditsReferencesAndRows() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new Files(); var owner = scope.Player.Associate!;
        owner.Mentor_ExpBalls = 111; owner.Mentor_Blessing = 222; owner.Mentor_Stones = 333;
        var original = new Associate.Member { UID = 21, Name = "Original" }; owner.Add(5, original);
        owner.Add(2, new Associate.Member { UID = 99 }); owner.MyUID = 123; //Pool key remains17.
        files.Write("Associate.txt", "Count=3\r\n" + Record(online: "not-numeric", bp: "also-ignored") + "\r\n" +
            Record(mode: 4, uid: 30, credit: 999, online: "garbage", bp: "garbage") + "\r\n" + Record(owner: 18, mode: 255, uid: 31, credit: 444));
        Associate.Load();
        Assert.Same(owner, Associate.Associates[17]); Assert.Equal(123u, owner.MyUID); Assert.Same(original, owner.Associat[5][21]);
        Assert.True(owner.Contain(2, 99)); Assert.True(owner.Contain(4, 30));
        Assert.Equal(111u, owner.Mentor_ExpBalls); Assert.Equal(222u, owner.Mentor_Blessing); Assert.Equal(333u, owner.Mentor_Stones);
        Assert.Equal(444u, Associate.Associates[18].Mentor_ExpBalls); Assert.True(Associate.Associates[18].Contain(255, 31));
        Assert.Null(Associate.Associates[18].MyClient); Assert.Empty(Associate.Associates[18].OnlineApprentice);
    });

    [Fact]
    public Task AssociateSaveReplacesOwnedFileRatherThanAppendingAndLoadDoesNotPublishEmptyOwners() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var files = new Files(); var owner = scope.Player.Associate!;
        owner.OnUse(); Associate.Save(); string path = Path.Combine(files.Root, "Associate.txt");
        Assert.Equal(new[] { "Count=0" }, File.ReadAllLines(path));
        Associate.Associates = new(); Associate.Load(); Assert.Empty(Associate.Associates);
        owner.Add(5, new Associate.Member { UID = 21, Name = "First" }); Associate.Save();
        Assert.Equal(2, File.ReadAllLines(path).Length);
        owner.Associat[5][21].Name = "Second"; Associate.Save();
        string[] lines = File.ReadAllLines(path); Assert.Equal(2, lines.Length); Assert.Equal("Count=1", lines[0]); Assert.Contains("/Second/", lines[1]); Assert.DoesNotContain("/First/", lines[1]);
    });
}
