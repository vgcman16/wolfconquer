namespace WolfConquer.Core.World;

// Entire native MsgEliteTournament.GroupTyp13..21. No outer tournament birth is invented.
// Later TeamHP metadata intake must reuse this sole declaration, not redeclare it.
internal enum LegacyNativeEliteTournamentGroupType : ushort
{
    EPK_Lvl100Minus,
    EPK_Lvl100To119,
    EPK_Lvl120To129,
    EPK_Lvl130Plus,
    Count
}
