using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMsgSpellAnimation
{
	public void Send(LegacyNativeMonsterRole monster)
	{
		if (Targets.Count <= 30)
		{
			monster.Send(CreateAnimation(Targets, _Stream));
			return;
		}
		Dictionary<uint, Queue<SpellObj>> dictionary = new Dictionary<uint, Queue<SpellObj>>();
		SpellObj[] array = Targets.ToArray();
		uint num = 0u;
		for (int i = 0; i < array.Length; i++)
		{
			if (i % 30 == 0)
			{
				num++;
				dictionary.Add(num, new Queue<SpellObj>());
			}
			dictionary[num].Enqueue(array[i]);
		}
		foreach (Queue<SpellObj> value in dictionary.Values)
		{
			monster.Send(CreateAnimation(value, _Stream));
		}
	}
}
