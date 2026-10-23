using WolfConquer.Core.Data;
namespace WolfConquer.Core.World;
internal partial class LegacyNativeEliteGroupRuntime
{
public class FighterStats
	{
		public enum StatusFlag : uint
		{
			None = 0u,
			Fighting = 1u,
			Lost = 2u,
			Qualified = 3u,
			Waiting = 4u,
			Bye = 5u,
			Inactive = 7u,
			WonMatch = 8u
		}

		public uint CrossEliteRank = 0u;

		public LegacyNativeDbSafeDictionary<uint, LegacyNativeGameClientRuntime>? Wagers;

		public string? Name;

		public uint UID;

		public uint RealUID;

		public uint Mesh;

		public uint Wager;

		public uint Cheers;

		public uint Points;

		public byte ClaimReward = 0;

		public uint ServerID = 0u;

		private StatusFlag _flg;

		public bool Advanced = false;

		public StatusFlag Flag
		{
			get
			{
				return _flg;
			}
			set
			{
				_flg = value;
				if (_flg == StatusFlag.Qualified)
				{
					Advanced = true;
				}
			}
		}

		public override string ToString()
		{
			LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
			writeLine.Add(Name!).Add(UID).Add(Mesh)
				.Add(ClaimReward);
			return writeLine.Close();
		}

		public FighterStats()
		{
		}

		public FighterStats(uint id, string name, uint mesh, uint _ServerID, uint _RealUID)
		{
			Wagers = new LegacyNativeDbSafeDictionary<uint, LegacyNativeGameClientRuntime>();
			UID = id;
			Name = name;
			ServerID = _ServerID;
			Mesh = mesh;
			RealUID = _RealUID;
		}

		public void Reset(bool setflag = false)
		{
			Wagers!.Clear();
			Wager = 0u;
			Points = 0u;
			Cheers = 0u;
			Flag = StatusFlag.None;
			if (setflag)
			{
				Flag = StatusFlag.None;
			}
		}

		public FighterStats Clone()
		{
			FighterStats fighterStats = new FighterStats(UID, Name!, Mesh, ServerID, RealUID);
			fighterStats.Points = Points;
			fighterStats.Flag = Flag;
			fighterStats.Wager = Wager;
			return fighterStats;
		}
	}
}
