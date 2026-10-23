namespace WolfConquer.Core.World;

// Complete native Role.Flags7..49 poker enum family.
internal static class LegacyNativePokerFlags
{
    public enum CardsType : byte { Heart, Spade, Club, Diamond }
    public enum PlayerType : byte { Player = 1, Watcher }
    public enum TableInteractiveType : byte { Join = 0, Leave = 1, Watch = 4 }
    public enum TableState : byte { Unopened, Pocket, Flop, Turn, River, ShowDown }
    public enum TableType : byte { TexasHoldem = 1, ShowHand }
    public enum TableUpdate : byte { Statue = 233, Chips, PlayerCount }
}
