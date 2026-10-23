using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeEquipItemStorage
{
    // Whole native Equip1884..1922. No UID snapshot, BP callback, skip/cold-only path or catch.
    public void SendMentorShare(LegacyNativePacket stream)
    {
        LegacyNativeMsgApprenticeInformation msgApprenticeInformation = LegacyNativeMsgApprenticeInformation.Create();
        msgApprenticeInformation.Mode = LegacyNativeMsgApprenticeInformation.Action.Mentor;
        if (Owner!.Player.MyMentor != null && Owner.Player.Associate!.Associat.ContainsKey(4) && Owner.Player.MyMentor.MyClient != null && Owner.Player.Associate.Associat[4].ContainsKey(Owner.Player.MyMentor.MyUID))
        {
            LegacyNativePlayerRuntime player = Owner.Player.MyMentor.MyClient.Player;
            Owner.Player.SetMentorBattlePowers(player.GetShareBattlePowers((uint)Owner.Player.RealBattlePower), (uint)player.RealBattlePower);
            msgApprenticeInformation.Mentor_ID = player.UID;
            msgApprenticeInformation.Apprentice_ID = Owner.Player.UID;
            msgApprenticeInformation.Enrole_date = (uint)Owner.Player.Associate.Associat[4][player.UID].Timer;
            msgApprenticeInformation.Fill(player.Owner);
            msgApprenticeInformation.Shared_Battle_Power = player.GetShareBattlePowers((uint)Owner.Player.RealBattlePower);
            msgApprenticeInformation.WriteString(player.Name, Owner.Player.Spouse, Owner.Player.Name, Owner.Player.Signature);
            Owner.Send(msgApprenticeInformation.GetArray(stream));
        }
        if (!Owner.Player.Associate!.Associat.ContainsKey(5)) return;
        foreach (LegacyNativeGameClientRuntime value in Owner.Player.Associate.OnlineApprentice.Values)
        {
            if (Owner.Player.Associate.Associat[5].ContainsKey(value.Player.UID))
            {
                LegacyNativePlayerRuntime player2 = value.Player;
                player2.SetMentorBattlePowers(Owner.Player.GetShareBattlePowers((uint)player2.RealBattlePower), (uint)Owner.Player.RealBattlePower);
                msgApprenticeInformation.Apprentice_ID = player2.UID;
                msgApprenticeInformation.Enrole_date = (uint)Owner.Player.Associate.Associat[5][player2.UID].Timer;
                msgApprenticeInformation.Level = (byte)Owner.Player.Level;
                msgApprenticeInformation.Class = Owner.Player.Class;
                msgApprenticeInformation.PkPoints = Owner.Player.PKPoints;
                msgApprenticeInformation.Mesh = Owner.Player.Mesh;
                msgApprenticeInformation.Online = 1u;
                msgApprenticeInformation.Shared_Battle_Power = Owner.Player.GetShareBattlePowers((uint)player2.RealBattlePower);
                msgApprenticeInformation.WriteString(Owner.Player.Name, Owner.Player.Spouse, player2.Name, player2.Signature);
                player2.Owner.Send(msgApprenticeInformation.GetArray(stream));
            }
        }
    }
}
