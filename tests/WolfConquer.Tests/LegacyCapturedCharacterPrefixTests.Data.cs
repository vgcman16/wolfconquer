using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyCapturedCharacterPrefixTests
{
    [Fact]
    public void CapturedCompleteCoreKeepsIndependentIdentitiesAndMissingOptionalDefaults()
    {
        var bytes = CharacterBytes();
        var image = Image(bytes);
        Assert.True(LegacyCapturedCharacterPrefix.TryCreate(image, out var captured, out var reason), reason);
        var value = Assert.IsType<LegacyCapturedCharacterPrefix>(captured);
        Assert.Same(image, value.Image);
        Assert.Equal(CapturedAccount, image.AccountId);
        Assert.Equal(CapturedCharacter, value.SavedPlayerValues.Uid);
        Assert.NotEqual((uint)image.AccountId, value.SavedPlayerValues.Uid);
        Assert.Equal(1U, value.SavedPlayerValues.Online);
        Assert.False(value.SavedPlayerValues.RelicSpirit);
        Assert.False(value.SavedPlayerValues.SpawinEffect);
        Assert.False(value.SavedPlayerValues.SpawinEffectSkills);
        Assert.Equal(0U, value.SavedPlayerValues.HeroPoints);
        Assert.Equal((byte)0, value.SavedPlayerValues.SkinUnlock);
        Assert.Equal("None", value.SavedPlayerValues.Spouse);
        Assert.False(value.BeastActivated);
        Assert.Equal(0U, value.BeastTotalPoints);
        Assert.Equal(0U, value.BeastFlag);
        Assert.Equal("0/", value.HairfaceLine);
        Assert.Equal(bytes, image.CopyCharacterBytes());
    }

    [Fact]
    public void PresentTypedScalarsRetainExactBoundariesAndManagedIntegerSyntax()
    {
        var text = AddFields(CompleteText(), "Spouse=Partner\r\nRelicSpirit= true \r\nSpawinEffect=False\r\n" +
            "SpawinEffectSkills=TRUE\r\nDeityLandLuckyPoints= +4294967295 \r\nHeroPoints= -0 \r\n" +
            "ClassExperience=17\r\nSavePromote=19\r\nSkinUnlock= +255 \r\nDoomosaurSkin= -0 \r\n" +
            "PrayingElfSkin=2\r\nPolarDragonSkin=3\r\nChargeChi=4\r\nChargeGuildSkill=5\r\n" +
            "TailedBeastActivated=TrUe\r\nTailedBeastTotalPoints= +1000 \r\nTailedBeastFlag=4294967295\r\n");
        text = text.Replace("Body=1003", "Body= +65535 ", StringComparison.Ordinal)
            .Replace("Face=0", "Face= -0 ", StringComparison.Ordinal)
            .Replace("Online=1", "Online= +4294967295 ", StringComparison.Ordinal);
        var captured = Capture(Encoding.UTF8.GetBytes(text));
        var v = captured.SavedPlayerValues;
        Assert.True(v.RelicSpirit); Assert.False(v.SpawinEffect); Assert.True(v.SpawinEffectSkills);
        Assert.Equal(uint.MaxValue, v.DeityLandLuckyPoints); Assert.Equal(0U, v.HeroPoints);
        Assert.Equal(17U, v.ClassExperience); Assert.Equal(19U, v.SavePromote);
        Assert.Equal(byte.MaxValue, v.SkinUnlock); Assert.Equal((byte)0, v.DoomosaurSkin);
        Assert.Equal((byte)2, v.PrayingElfSkin); Assert.Equal((byte)3, v.PolarDragonSkin);
        Assert.Equal((byte)4, v.ChargeChi); Assert.Equal((byte)5, v.ChargeGuildSkill);
        Assert.Equal(ushort.MaxValue, v.Body); Assert.Equal((ushort)0, v.Face);
        Assert.Equal(uint.MaxValue, v.Online); Assert.Equal("Partner", v.Spouse);
        Assert.True(captured.BeastActivated); Assert.Equal(1000U, captured.BeastTotalPoints);
        Assert.Equal(uint.MaxValue, captured.BeastFlag);
        Assert.Contains("Online= 1 \r\n", Encoding.UTF8.GetString(captured.CopyOnlineCharacterBytes()));
    }

    [Fact]
    public void InputAndReturnedBuffersAndSnapshotDictionariesCannotChangeRetainedData()
    {
        var bytes = CharacterBytes("RelicSpirit=True\r\n");
        var original = bytes.ToArray();
        var captured = Capture(bytes);
        bytes.AsSpan().Fill(0x78);
        var snapshot = captured.Snapshot;
        Assert.IsType<Dictionary<string, string>>(snapshot.Attributes)["Name"] = "foreign";
        Assert.IsType<Dictionary<string, string>>(snapshot.PaidNobility)["Injected"] = "1";
        var output = captured.CopyOnlineCharacterBytes(); output.AsSpan().Fill(0x79);
        Assert.Equal("SqlStateSynthetic", captured.Snapshot.Attributes["Name"]);
        Assert.Empty(captured.Snapshot.PaidNobility);
        Assert.Equal(original, captured.Image.CopyCharacterBytes());
        Assert.Equal(original, captured.CopyOnlineCharacterBytes());
        Assert.True(captured.SavedPlayerValues.RelicSpirit);
    }
}
