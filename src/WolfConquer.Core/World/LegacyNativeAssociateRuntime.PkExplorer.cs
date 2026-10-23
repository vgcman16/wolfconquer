using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// Whole native Associate method family, reusing incoming genuine physical owners.
internal partial class LegacyNativeAssociateRuntime
{
    public partial class MyAsociats
    {
		public uint GetTimePkExplorer()
		{
			uint num = 0u;
			DateTime now = DateTime.Now;
			return (uint)(now.Month * 1000000 + now.Day * 10000 + now.Hour * 100 + now.Minute);
		}

		public void AddPKExplorer(LegacyNativeGameClientRuntime Owner, LegacyNativePlayerRuntime client)
		{
			if (!OnUse())
			{
				return;
			}
			Member member = new Member
			{
				UID = client.UID,
				Timer = GetTimePkExplorer(),
				Name = client.Name
			};
			member.Timer = GetTimePkExplorer();
			member.KillsCount++;
			member.KillMap = client.Map;
			if (Associat.ContainsKey(6))
			{
				if (Associat[6].TryGetValue(member.UID, out var value))
				{
					value.Timer = GetTimePkExplorer();
					value.KillsCount++;
					value.KillMap = client.Map;
					return;
				}
				if (AllowAdd(6, member.UID, 50))
				{
					Associat[6].TryAdd(member.UID, member);
					return;
				}
				Member member2 = Associat[6].Values.ToArray()[0];
				Remove(6, member2.UID);
				if (AllowAdd(6, member.UID, 50))
				{
					Add(6, member);
				}
			}
			else
			{
				Associat.TryAdd(6, new ConcurrentDictionary<uint, Member>());
				Associat[6].TryAdd(member.UID, member);
			}
		}

		public Member[] GetPkExplorerRank()
		{
			if (Associat.ContainsKey(6))
			{
				return Associat[6].Values.OrderBy((Member kill) => kill.KillsCount).ToArray();
			}
			return new Member[0];
		}
    }
}
