using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    public void AddPoints(uint _Points)
    {
        var owner = user;
        var player = owner?.Player;
        if (owner is null || player is null || !ReferenceEquals(player.Owner, owner) || !IsCombatOwnedBy(owner))
            throw new InvalidOperationException("Ninja points require the current constructor-owned player.");
        // Reject an impossible credit before a packet loan, native mutation or send.
        uint beforeExp = Exp;
        uint updatedExp = checked(beforeExp + _Points);
        using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
        LegacyNativePacket stream = recycledPacket.GetStream();
        LegacyNativeMsgNinja.MsgNinjaUser msgNinjaUser = new LegacyNativeMsgNinja.MsgNinjaUser();
        msgNinjaUser.Type = 2u;
        msgNinjaUser.Points = updatedExp;
        stream.CreateNinjaUser(msgNinjaUser);
        if (Exp != beforeExp || !ReferenceEquals(owner.Player, player) ||
            !ReferenceEquals(player.Owner, owner) || !IsCombatOwnedBy(owner))
            throw new InvalidOperationException("Ninja points owner changed during preparation.");
        Exp = updatedExp;
        owner.Send(stream);
    }
}
