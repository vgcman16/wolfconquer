namespace WolfConquer.Tests;

public sealed partial class LegacyDeitylandChestCapacitySafetyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public Task RegisteredEpicChestAcceptsOnlyEpicKeyWithoutRareKey(byte firstBound) => RunAsync(async fixture =>
    {
        fixture.Seed(40, firstStack: 1, secondStack: 2, firstBound,
            firstKeyItemId: 3_303_974, secondKeyItemId: 3_303_974, npcId: 22_607);
        await fixture.UnlockEpicAsync();
        fixture.AssertEpicOutcome(keyIndex: 0);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public Task RegisteredEpicChestRefusesRareOnlyWithoutDebitRewardOrSave(byte firstBound) => RunAsync(async fixture =>
    {
        fixture.Seed(40, firstStack: 1, secondStack: 2, firstBound, npcId: 22_607);
        var before = fixture.Session;
        await fixture.UnlockEpicAsync();
        fixture.AssertUnchanged(before);
        Assert.Empty(fixture.Sent);
        Assert.Contains("keyItemId=3303974; key missing.", fixture.Log.ToString());
        Assert.DoesNotContain("chest item reward", fixture.Log.ToString());
        Assert.DoesNotContain("chest CP reward", fixture.Log.ToString());
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public Task RegisteredEpicChestPreservesEarlierRareKeyAndDebitsLaterEpicKey(byte firstBound) => RunAsync(async fixture =>
    {
        fixture.Seed(40, firstStack: 1, secondStack: 1, firstBound,
            firstKeyItemId: 3_303_973, secondKeyItemId: 3_303_974, npcId: 22_607);
        await fixture.UnlockEpicAsync();
        fixture.AssertEpicOutcome(keyIndex: 1);
    });
}
