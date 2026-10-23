namespace WolfConquer.Core.World;

// Native Pool52/54/56 declaration order. No startup/ranking publication installed.
internal static class LegacyNativePoolFlowers
{
    public static LegacyNativeFlowersRuntime.FlowersRankingToday FlowersRankToday = new LegacyNativeFlowersRuntime.FlowersRankingToday();
    public static LegacyNativeFlowersRuntime.FlowerRanking GirlsFlowersRanking = new LegacyNativeFlowersRuntime.FlowerRanking();
    public static LegacyNativeFlowersRuntime.FlowerRanking BoysFlowersRanking = new LegacyNativeFlowersRuntime.FlowerRanking(_Girls: false);
}
