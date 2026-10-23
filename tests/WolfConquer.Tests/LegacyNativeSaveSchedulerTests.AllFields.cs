using System.Reflection;
using WolfConquer.Core.World;
namespace WolfConquer.Tests;
public sealed partial class LegacyNativeSaveSchedulerTests
{
    [Fact]
    public Task EveryManifestPlayerSavedFieldHasItsExactNativeTypeAndBirthDefault() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        var cases = new (string Name, Type Type)[]
        {
            ("AnimaSkin", typeof(byte)),
            ("Avatar", typeof(ushort)),
            ("PinCodeAnima", typeof(string)),
            ("color", typeof(uint)),
            ("weatherType", typeof(uint)),
            ("BannedChatStamp", typeof(DateTime)),
            ("PermenantBannedChat", typeof(bool)),
            ("IsBannedChat", typeof(bool)),
            ("Conquer", typeof(uint)),
            ("Coonquer14", typeof(uint)),
            ("BanCount", typeof(uint)),
            ("CanChangeWindWalkerFree", typeof(DateTime)),
            ("UseChiToken", typeof(byte)),
            ("GiveFlowersToPerformer", typeof(int)),
            ("JoinPowerArenaStamp", typeof(DateTime)),
            ("ClaimTowerAmulets", typeof(byte)),
            ("TOMClaimTeamReward", typeof(byte)),
            ("MyTowerOfMysteryLayer", typeof(byte)),
            ("MyTowerOfMysteryLayerElite", typeof(byte)),
            ("TowerOfMysterychallenge", typeof(byte)),
            ("TowerOfMysteryChallengeFlag", typeof(uint)),
            ("TOMSelectChallengeToday", typeof(byte)),
            ("TOMChallengeToday", typeof(byte)),
            ("TOMRefreshReward", typeof(uint)),
            ("ItemRewordChristmas", typeof(uint)),
            ("TOM_Reward", typeof(LegacyNativeTowerRewardType)),
            ("OpenHousePack", typeof(byte)),
            ("botjail", typeof(byte)),
            ("ChargePromotion", typeof(byte)),
            ("RechargeProgress", typeof(LegacyNativePlayerRuntime.RechargeType)),
            ("VotePoints", typeof(byte)),
            ("BuyKingdomDeeds", typeof(byte)),
            ("KingDomDeeds", typeof(uint)),
            ("AtiveQuestApe", typeof(uint)),
            ("DailySignUpDays", typeof(ulong)),
            ("DailySignUpRewards", typeof(byte)),
            ("DailyMonth", typeof(byte)),
            ("NameEditCount", typeof(ushort)),
            ("DailySpiritBeadItem", typeof(uint)),
            ("DonatePoints", typeof(uint)),
            ("Goldprizesave", typeof(uint)),
            ("RamdanBag", typeof(uint)),
            ("itemRamdanBag", typeof(uint)),
            ("RewardPoints", typeof(uint)),
            ("DbTry", typeof(bool)),
            ("LotteryEntries", typeof(byte)),
            ("EpicQuestChance", typeof(uint)),
            ("OnlineMinutes", typeof(uint)),
            ("OnlinePointsPK", typeof(uint)),
            ("TournamentKills", typeof(uint)),
            ("HitShoot", typeof(uint)),
            ("MisShoot", typeof(uint)),
            ("ArenaDeads", typeof(uint)),
            ("ArenaKills", typeof(uint)),
            ("TCCaptainTimes", typeof(uint)),
            ("MyKillerUID", typeof(uint)),
            ("MyKillerName", typeof(string)),
            ("BlessTime", typeof(uint)),
            ("Day", typeof(int)),
            ("EnlightenReceive", typeof(ushort)),
            ("PMapX", typeof(ushort)),
            ("PMapY", typeof(ushort)),
            ("PMap", typeof(uint)),
            ("ExpBallUsed", typeof(byte)),
            ("BDExp", typeof(byte)),
            ("JoinOnflineTG", typeof(DateTime)),
            ("HuntingBlessing", typeof(uint)),
            ("FootballTeamID", typeof(int)),
            ("FootBallMatchPoints", typeof(uint)),
            ("MyFootBallPoints", typeof(uint)),
            ("SpecialGarment", typeof(uint)),
            ("RacePotions", typeof(LegacyNativeMsgSteedRace.UsableRacePotion[])),
        };
        foreach (var row in cases)
        {
            var field = typeof(LegacyNativePlayerRuntime).GetField(row.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
            Assert.NotNull(field); Assert.Equal(row.Type, field.FieldType);
            if (row.Name == "CanChangeWindWalkerFree") continue; // Bounded actual clock birth asserted separately.
            object? expected = row.Type.IsValueType ? Activator.CreateInstance(row.Type) : null;
            if (row.Name == "PinCodeAnima") expected = "";
            if (row.Name == "TowerOfMysterychallenge") expected = (byte)3;
            Assert.Equal(expected, field.GetValue(s.Client.Player));
        }
    });
}
