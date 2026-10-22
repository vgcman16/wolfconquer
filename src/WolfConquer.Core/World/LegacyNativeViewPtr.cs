#nullable disable

using System.Collections.Generic;

namespace WolfConquer.Core.World;

// Full native ViewPtr: live lists, duplicate references and unchecked casts retained.
internal class LegacyNativeViewPtr
{
    private List<ILegacyNativeMapObj>[] Objects;

    public LegacyNativeViewPtr()
    {
        Objects = new List<ILegacyNativeMapObj>[7];
        for (int i = 0; i < 7; i++)
        {
            Objects[i] = new List<ILegacyNativeMapObj>();
        }
    }

    public void AddObject<T>(T obj) where T : ILegacyNativeMapObj
    {
        Objects[(uint)obj.ObjType].Add(obj);
    }

    public void RemoveObject<T>(T obj) where T : ILegacyNativeMapObj
    {
        Objects[(uint)obj.ObjType].Remove(obj);
    }

    public bool ContainObject(LegacyNativeMapObjectType obj_t, uint UID)
    {
        for (int i = 0; i < Objects[(uint)obj_t].Count; i++)
        {
            List<ILegacyNativeMapObj> list = Objects[(uint)obj_t];
            if (i >= list.Count)
            {
                break;
            }
            if (list[i] != null && list[i].UID == UID)
            {
                return true;
            }
        }
        return false;
    }

    public bool TryGetObject<T>(LegacyNativeMapObjectType obj_t, uint UID, out T obj)
    {
        for (int i = 0; i < Objects[(uint)obj_t].Count; i++)
        {
            List<ILegacyNativeMapObj> list = Objects[(uint)obj_t];
            if (i >= list.Count)
            {
                break;
            }
            if (list[i] != null && list[i].UID == UID)
            {
                obj = (T)list[i];
                return true;
            }
        }
        obj = default(T);
        return false;
    }

    public List<ILegacyNativeMapObj> GetObjects(LegacyNativeMapObjectType typ)
    {
        return Objects[(uint)typ];
    }

    public void Clear(LegacyNativeMapObjectType typ)
    {
        Objects[(uint)typ].Clear();
    }
}
