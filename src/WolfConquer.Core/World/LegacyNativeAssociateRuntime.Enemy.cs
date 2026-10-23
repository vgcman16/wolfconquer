using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native Associate method family, reusing incoming genuine physical owners.
internal partial class LegacyNativeAssociateRuntime
{
    public partial class MyAsociats
    {
		public void AddEnemy(LegacyNativeGameClientRuntime Owner, LegacyNativePlayerRuntime Killer)
		{
			Member member = new Member
			{
				UID = Killer.UID,
				Name = Killer.Name,
				Class = Killer.Class,
				Signature = Killer.Signature,
				Mesh = Killer.Mesh,
				Level = Killer.Level,
				NobilityRank = (uint)Killer.NobilityRank
			};
			if (AllowAdd(2, Killer.UID, 20))
			{
				Add(2, member);
			}
			else
			{
				Member member2 = Associat[2].Values.ToArray()[0];
				Remove(2, member2.UID);
				if (AllowAdd(2, Killer.UID, 20))
				{
					Add(2, member);
				}
			}
			using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
			LegacyNativePacket stream = recycledPacket.GetStream();
			Owner.Send(stream.KnowPersonsCreate(LegacyNativeKnowPersonsPacket.Action.AddEnemy, Killer.UID, online: true, (uint)Killer.NobilityRank, 0u, Killer.Mesh, Killer.Level, Killer.Signature, Killer.Name));
		}
    }
}
