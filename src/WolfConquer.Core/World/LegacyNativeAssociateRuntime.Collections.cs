using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeAssociateRuntime
{
    public partial class MyAsociats
    {
        public bool OnUse()
        {
            if (MyUID != 0)
            {
                if (!Associates.ContainsKey(MyUID)) Associates.TryAdd(MyUID, this);
                return true;
            }
            return false;
        }

        public bool Contain(byte Mode, uint UID)
        {
            if (Associat.ContainsKey(Mode) && Associat[Mode].ContainsKey(UID)) return true;
            return false;
        }

        public bool Remove(byte Mode, uint UID)
        {
            if (!Associat.ContainsKey(Mode)) return false;
            if (!Associat[Mode].ContainsKey(UID)) return false;
            Member? value;
            return Associat[Mode].TryRemove(UID, out value);
        }

        public bool AllowAdd(byte Mode, uint UID, byte amout)
        {
            if (!Associat.ContainsKey(Mode)) return true;
            if (Associat[Mode].ContainsKey(UID)) return false;
            if (Associat[Mode].Count < amout) return true;
            return false;
        }

        public void Add(byte mode, Member member)
        {
            if (OnUse())
            {
                if (Associat.ContainsKey(mode))
                {
                    Associat[mode].TryAdd(member.UID, member);
                    return;
                }
                Associat.TryAdd(mode, new ConcurrentDictionary<uint, Member>());
                Associat[mode].TryAdd(member.UID, member);
            }
        }
    }
}
