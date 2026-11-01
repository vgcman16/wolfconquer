using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Fact]
    public void AdmissionMapCopyPreservesOpaqueCaseAndRejectsAmbiguousOwnedField()
    {
        var source = new Dictionary<string, string>(StringComparer.Ordinal)
        { ["Online"] = "0", ["Other"] = "UP", ["other"] = "lower" };
        var copy = LegacyOrdinaryAdmissionImages.CopyWithOwnedField(source, "Online", "1");
        Assert.Same(StringComparer.Ordinal, copy.Comparer);
        Assert.Equal("UP", copy["Other"]); Assert.Equal("lower", copy["other"]);
        Assert.Equal("1", copy["Online"]); Assert.Equal("0", source["Online"]);
        source["online"] = "0";
        Assert.Throws<InvalidDataException>(() => LegacyOrdinaryAdmissionImages.CopyWithOwnedField(source, "Online", "1"));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public Task PendingExperienceIsHeldBeforeOrdinaryAdmissionEffects(bool malformed, bool caseDistinct) => Run(async f =>
    {
        // This is a declared saved-input control for refusal only. A DATA
        // marker does not issue an award or an admission/resume owner.
        var state = LegacyExperienceContinuation.CreateCredit(CreationFixture.RuneActor,
            CreationFixture.RuneActor, Guid.Parse("146e30e7-1041-47b4-a144-d21db7e910a4"), 139, 123, 7);
        var text = File.ReadAllText(f.Data.UserPath)
            .Replace("Level=140\r\nExperience=123\r\n", "Level=139\r\nExperience=130\r\n", StringComparison.Ordinal)
            .Replace("Online=0\r\n", "Online=0\r\nExperienceContinuation=" +
                (malformed ? "malformed" : LegacyExperienceContinuationCodec.Encode(state)) + "\r\n" +
                (caseDistinct ? "Other=UP\r\nother=lower\r\n" : ""), StringComparison.Ordinal);
        File.WriteAllText(f.Data.UserPath, text, new UTF8Encoding(true));
        var before = f.Data.Files();
        var connection = f.NewConnection(CreationFixture.RuneActor);
        var client = Assert.IsType<LegacyNativeGameClientRuntime>(connection.NativeGameClient);
        await f.SendOnAsync(connection, new LegacyMsgLoginGamePacket(CreationFixture.RuneActor).ToFrameBytes());

        Assert.False(f.Store.TryGet(connection, out _));
        Assert.Null(client.OrdinarySpellBinding);
        Assert.Empty(f.Frames(CreationFixture.RuneActor));
        Assert.Contains(malformed ? "experienceAdmissionMarkerInvalid" : "held pending experience before admission",
            f.Log.ToString(), StringComparison.Ordinal);
        f.Data.AssertFiles(before);
    }, enter: false);

    [Fact]
    public Task OrdinaryAdmissionCurrentlyRefusesCaseDistinctSavedCharacterKeysWithoutChangingFiles() => Run(async f =>
    {
        var original = File.ReadAllText(f.Data.UserPath);
        File.WriteAllText(f.Data.UserPath, original.Replace("Online=0\r\n",
            "Online=0\r\nOther=UP\r\nother=lower\r\n", StringComparison.Ordinal), new UTF8Encoding(true));
        var before = f.Data.Files();
        var connection = f.NewConnection(CreationFixture.RuneActor);
        await f.SendOnAsync(connection, new LegacyMsgLoginGamePacket(CreationFixture.RuneActor).ToFrameBytes());
        Assert.False(f.Store.TryGet(connection, out _));
        Assert.Null(Assert.IsType<LegacyNativeGameClientRuntime>(connection.NativeGameClient).OrdinarySpellBinding);
        Assert.Empty(f.Frames(CreationFixture.RuneActor));
        f.Data.AssertFiles(before);
    }, enter: false);
}
