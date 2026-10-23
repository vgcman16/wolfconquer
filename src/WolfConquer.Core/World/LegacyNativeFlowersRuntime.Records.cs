using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native Flowers356..382; real owner/method family.
internal partial class LegacyNativeFlowersRuntime : IEnumerable<LegacyNativeFlowersRuntime.Flower>, IEnumerable
{
	public IEnumerator<Flower> GetEnumerator()
	{
		yield return RedRoses;
		yield return Lilies;
		yield return Orchids;
		yield return Tulips;
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	public override string ToString()
	{
		LegacyNativeDbWriteLine writeLine = new LegacyNativeDbWriteLine('/');
		writeLine.Add(FreeFlowers);
		using (IEnumerator<Flower> enumerator = GetEnumerator())
		{
			while (enumerator.MoveNext())
			{
				Flower current = enumerator.Current;
				writeLine.Add(current.Amount).Add(current.Amount2day);
			}
		}
		return writeLine.Close();
	}
}
