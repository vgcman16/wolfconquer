using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

// Whole native Associate method family, reusing incoming genuine physical owners.
internal partial class LegacyNativeAssociateRuntime
{
    public partial class MyAsociats
    {
		public IEnumerable<string> ToStringMember()
		{
			foreach (KeyValuePair<byte, ConcurrentDictionary<uint, Member>> typ in Associat)
			{
				foreach (Member member in typ.Value.Values)
				{
					yield return MyUID + "/" + typ.Key + "/" + Mentor_ExpBalls + "/" + Mentor_Blessing + "/" + Mentor_Stones + "/" + member.ToString();
				}
			}
		}
    }
}
