using System.Globalization;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeEpicTrojanTests
{
    [Fact]
    public void SaveUsesLiteralTwentyFieldOrderAndNeverChangesPhysicalArraysOrScalars()
    {
        using var culture = new CultureScope();
        var player = Seed(1);
        var arrays = Arrays(player); var scalars = Scalars(player);
        Assert.Equal(Sequence, player.SaveEpicTrojan());
        Assert.Equal(Enumerable.Range(1, 20).Select(value => (uint)value), Values(player));
        Assert.Equal(scalars, Scalars(player)); SameArrays(player, arrays);
    }

    [Fact]
    public void LoadUsesExistingArraysAndIgnoresExtraTokensWithOrWithoutTrailingSlash()
    {
        foreach (string line in new[] { Sequence, Sequence.TrimEnd('/'), Sequence + "999/256/ignored/" })
        {
            var player = Seed(); var arrays = Arrays(player);
            player.LoadEpicTrojan(line);
            Assert.Equal(Enumerable.Range(1, 20).Select(value => (uint)value), Values(player));
            Assert.Equal((byte)201, player.EpicTrijanKillGhostReaver);
            SameArrays(player, arrays);
        }
    }

    [Fact]
    public void SaveLoadAcrossActualPlayersPreservesOmittedGhostAndIndependentArraysThenResetClearsGhost()
    {
        using var culture = new CultureScope();
        var source = Seed(1); var loaded = Player();
        loaded.EpicTrijanKillGhostReaver = 254;
        loaded.LoadEpicTrojan(source.SaveEpicTrojan());
        Assert.Equal(Sequence, loaded.SaveEpicTrojan());
        Assert.Equal((byte)254, loaded.EpicTrijanKillGhostReaver);
        Assert.Equal((byte)201, source.EpicTrijanKillGhostReaver);
        for (int i = 0; i < 4; i++) Assert.NotSame(Arrays(source)[i], Arrays(loaded)[i]);
        loaded.EpicTrojanItemChange[0] = 77;
        Assert.Equal(1u, source.EpicTrojanItemChange[0]);
        loaded.ResetEpicTrojan();
        Assert.Equal((byte)0, loaded.EpicTrijanKillGhostReaver);
        Assert.Equal(77u, loaded.EpicTrojanItemChange[0]);
        Assert.Equal(Sequence, source.SaveEpicTrojan());
    }

    [Fact]
    public void NullLoadDoesNothingEvenWithNullEmptyShortAndAliasedPhysicalArrays()
    {
        var player = Seed(); var shared = new uint[] { 87 };
        player.EpicTrojanItemChange = null!;
        player.EpicTrojanArrayItemChange = [];
        player.EpicTrojanMr_MirrorItemChange = shared;
        player.EpicTrojanGeneralPakItemChange = shared;
        var scalars = Scalars(player); var arrays = Arrays(player);
        player.LoadEpicTrojan(null);
        Assert.Equal(scalars, Scalars(player)); SameArrays(player, arrays);
        Assert.Equal(new uint[] { 87 }, shared); Assert.Empty(player.EpicTrojanArrayItemChange);
    }

    [Fact]
    public void EmptyMalformedAndNonDelimiterLinesUseTwentyZeroDefaultsNotBirthMaterialFlags()
    {
        foreach (string line in new[] { "", "1", "garbage", "1,2,3", "/", "//", "bad/bad/" })
        {
            var player = Seed(); var arrays = Arrays(player);
            player.LoadEpicTrojan(line);
            Assert.Equal(new uint[20], Values(player));
            Assert.Equal((byte)201, player.EpicTrijanKillGhostReaver);
            SameArrays(player, arrays);
        }
    }

    [Fact]
    public void TruncatedRecordKeepsParsedPrefixButOverwritesEveryMissingFieldWithZero()
    {
        var player = Seed();
        player.LoadEpicTrojan("71/72/73/74/75/");
        Assert.Equal(new uint[] { 71, 72, 73, 74, 75 }.Concat(new uint[15]), Values(player));
        Assert.Equal((byte)0, player.CanChangeArrayEpicMaterial);
        Assert.Equal((byte)0, player.CanChangeMr_MirrorEpicMaterial);
        Assert.Equal((byte)0, player.CanChangGeneralPakMaterial);
        Assert.Equal((byte)201, player.EpicTrijanKillGhostReaver);
    }

    [Fact]
    public void MalformedTokensAreConsumedInPlaceAndLaterGoodTokensKeepTheirNativePositions()
    {
        var player = Seed();
        player.LoadEpicTrojan("1/bad/3/4294967296/256/255/7/8/-1/10/11/12/13/14/15/16/17/18/19/20/");
        Assert.Equal(new uint[] { 1, 0, 3, 0, 0, 255, 7, 8, 0, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20 }, Values(player));
        Assert.Equal((byte)255, player.CanChangeArrayEpicMaterial); // Flags are raw bytes, not booleans.
    }

    [Fact]
    public void UIntAndByteMaximaPersistAtEveryNativeWidthWithoutSignedNarrowing()
    {
        using var culture = new CultureScope();
        var tokens = Tokens(); var expected = new uint[20];
        foreach (int slot in UIntSlots) { tokens[slot] = "4294967295"; expected[slot] = uint.MaxValue; }
        foreach (int slot in ByteSlots) { tokens[slot] = "255"; expected[slot] = byte.MaxValue; }
        string line = string.Join('/', tokens) + "/";
        var player = Player(); player.LoadEpicTrojan(line);
        Assert.Equal(expected, Values(player));
        Assert.Equal(line, player.SaveEpicTrojan());
    }

    [Fact]
    public void EveryByteFieldRejectsOverflowAndNegativeInputToZeroWithoutClampingOrWrapping()
    {
        foreach (string bad in new[] { "256", "-1", "4294967295", "999999999999999999999" })
        {
            var tokens = Tokens();
            var expected = Enumerable.Range(1, 20).Select(value => (uint)value).ToArray();
            foreach (int slot in ByteSlots) { tokens[slot] = bad; expected[slot] = 0; }
            var player = Seed(); player.LoadEpicTrojan(string.Join('/', tokens) + "/");
            Assert.Equal(expected, Values(player)); Assert.Equal((byte)201, player.EpicTrijanKillGhostReaver);
        }
    }

    [Fact]
    public void EveryUIntFieldRejectsOverflowNegativeAndNonDecimalInputToZeroWithoutStoppingTheRecord()
    {
        foreach (string bad in new[] { "4294967296", "-1", "0xFF", "1e3" })
        {
            var tokens = Tokens();
            var expected = Enumerable.Range(1, 20).Select(value => (uint)value).ToArray();
            foreach (int slot in UIntSlots) { tokens[slot] = bad; expected[slot] = 0; }
            var player = Seed(); player.LoadEpicTrojan(string.Join('/', tokens) + "/");
            Assert.Equal(expected, Values(player));
        }
    }

    [Fact]
    public void RealDbLineParsingUsesCurrentCulturePositiveSignAndNativeUnsignedSaveFormatting()
    {
        using var culture = new CultureScope();
        var custom = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        custom.NumberFormat.PositiveSign = "p";
        CultureInfo.CurrentCulture = custom;
        var line = string.Join('/', Tokens().Select(value => " p" + value + " ")) + "/";
        var player = Player(); player.LoadEpicTrojan(line);
        Assert.Equal(Enumerable.Range(1, 20).Select(value => (uint)value), Values(player));
        Assert.Equal(Sequence, player.SaveEpicTrojan());
    }

    [Fact]
    public void SaveAndLoadOnlyTouchTheFirstTwoElementsOfLongerPhysicalArrays()
    {
        using var culture = new CultureScope();
        var player = Seed(1);
        player.EpicTrojanItemChange = [101, 102, 901];
        player.EpicTrojanArrayItemChange = [108, 109, 902];
        player.EpicTrojanMr_MirrorItemChange = [112, 113, 903];
        player.EpicTrojanGeneralPakItemChange = [116, 117, 904];
        var arrays = Arrays(player);
        player.LoadEpicTrojan(Sequence);
        Assert.Equal(Sequence, player.SaveEpicTrojan());
        Assert.Equal(new uint[] { 901, 902, 903, 904 }, Arrays(player).Select(array => array![2]));
        SameArrays(player, arrays);
    }
}
