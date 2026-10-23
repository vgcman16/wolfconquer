using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Whole native Associate method family, reusing incoming genuine physical owners.
internal partial class LegacyNativeAssociateRuntime
{
    public partial class Member
    {
		public ushort BattlePower
		{
			get
			{
				if (LegacyNativePoolClients.GamePoll!.TryGetValue(UID, out var value))
				{
					return (ushort)value.Player.BattlePower;
				}
				return 0;
			}
		}

		public uint IsOnline
		{
			get
			{
				if (LegacyNativePoolClients.GamePoll!.ContainsKey(UID))
				{
					return 1u;
				}
				return 0u;
			}
		}

		public override string ToString()
		{
			LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
			writeLine.Add(UID).Add(Timer).Add(ExpBalls)
				.Add(Stone)
				.Add(Blessing)
				.Add(Name)
				.Add(KillsCount)
				.Add(IsOnline)
				.Add(BattlePower)
				.Add(KillMap)
				.Add(Mesh)
				.Add(Level)
				.Add(NobilityRank)
				.Add(Signature)
				.Add(Class);
			return writeLine.Close();
		}
    }
}
