#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;

namespace WolfConquer.Core.World;

// Full native MapView component; no GameMap/RoleView/Player adoption or host route.
internal class LegacyNativeMapView
{
    private const int CELLS_PER_BLOCK = 18;

    private LegacyNativeCounter CounterMovement = new LegacyNativeCounter(1u);

    public LegacyNativeViewPtr[,] m_setBlock;

    private int Width;

    private int Height;

    private int GetWidthOfBlock()
    {
        return (Width - 1) / 18 + 1;
    }

    private int GetHeightOfBlock()
    {
        return (Height - 1) / 18 + 1;
    }

    public LegacyNativeMapView(int _Width, int _Height)
    {
        Width = _Width;
        Height = _Height;
        m_setBlock = new LegacyNativeViewPtr[GetWidthOfBlock(), GetHeightOfBlock()];
        for (int i = 0; i < GetWidthOfBlock(); i++)
        {
            for (int j = 0; j < GetHeightOfBlock(); j++)
            {
                m_setBlock[i, j] = new LegacyNativeViewPtr();
            }
        }
    }

    private int Block(int nPos)
    {
        return nPos / 18;
    }

    private LegacyNativeViewPtr BlockSet(int nPosX, int nPosY)
    {
        return m_setBlock[Block(nPosX), Block(nPosY)];
    }

    public bool MoveTo<T>(T obj, int nNewPosX, int nNewPosY) where T : ILegacyNativeMapObj
    {
        int x = obj.X;
        int y = obj.Y;
        if (x < 0 || x >= Width)
        {
            return false;
        }
        if (y < 0 || y >= Height)
        {
            return false;
        }
        if (nNewPosX < 0 || nNewPosX >= Width)
        {
            return false;
        }
        if (nNewPosY < 0 || nNewPosY >= Height)
        {
            return false;
        }
        if (Block(x) == Block(nNewPosX) && Block(y) == Block(nNewPosY))
        {
            return false;
        }
        BlockSet(x, y).RemoveObject(obj);
        BlockSet(nNewPosX, nNewPosY).AddObject(obj);
        if (obj.ObjType == LegacyNativeMapObjectType.Player)
        {
            obj.IndexInScreen = CounterMovement.Next;
        }
        return true;
    }

    public bool EnterMap<T>(T obj) where T : ILegacyNativeMapObj
    {
        if (obj.X < 0 || obj.X >= Width)
        {
            return false;
        }
        if (obj.Y < 0 || obj.Y >= Height)
        {
            return false;
        }
        BlockSet(obj.X, obj.Y).AddObject(obj);
        if (obj.ObjType == LegacyNativeMapObjectType.Player)
        {
            obj.IndexInScreen = CounterMovement.Next;
        }
        return true;
    }

    public bool LeaveMap<T>(T obj) where T : ILegacyNativeMapObj
    {
        if (obj.X < 0 || obj.X >= Width)
        {
            return false;
        }
        if (obj.Y < 0 || obj.Y >= Height)
        {
            return false;
        }
        BlockSet(obj.X, obj.Y).RemoveObject(obj);
        return true;
    }

    public IEnumerable<ILegacyNativeMapObj> Roles(LegacyNativeMapObjectType typ, int X, int Y,
        Predicate<ILegacyNativeMapObj> P = null)
    {
        for (int x = Math.Max(Block(X) - 1, 0); x <= Block(X) + 1 && x < GetWidthOfBlock(); x++)
        {
            for (int y = Math.Max(Block(Y) - 1, 0); y <= Block(Y) + 1 && y < GetHeightOfBlock(); y++)
            {
                ILegacyNativeMapObj[] list;
                try
                {
                    list = m_setBlock[x, y].GetObjects(typ).ToArray();
                }
                catch
                {
                    continue;
                }
                if (list == null)
                {
                    continue;
                }
                foreach (ILegacyNativeMapObj element in list)
                {
                    if (element == null)
                    {
                        continue;
                    }
                    if (P != null)
                    {
                        if (P(element))
                        {
                            yield return element;
                        }
                    }
                    else if (element != null)
                    {
                        yield return element;
                    }
                }
            }
        }
    }

    public int CountRoles(LegacyNativeMapObjectType typ, int X, int Y)
    {
        int num = 0;
        for (int i = Math.Max(Block(X) - 1, 0); i <= Block(X) + 1 && i < GetWidthOfBlock(); i++)
        {
            for (int j = Math.Max(Block(Y) - 1, 0); j <= Block(Y) + 1 && j < GetHeightOfBlock(); j++)
            {
                List<ILegacyNativeMapObj> objects = m_setBlock[i, j].GetObjects(typ);
                num += objects.Count;
            }
        }
        return num;
    }

    public IEnumerable<ILegacyNativeMapObj> GetAllMapRoles(LegacyNativeMapObjectType typ,
        Predicate<ILegacyNativeMapObj> P = null)
    {
        for (int x = 0; x < GetWidthOfBlock(); x++)
        {
            for (int y = 0; y < GetHeightOfBlock(); y++)
            {
                List<ILegacyNativeMapObj> list = m_setBlock[x, y].GetObjects(typ);
                for (int i = 0; i < list.Count && i < list.Count; i++)
                {
                    ILegacyNativeMapObj element = list[i];
                    if (element == null)
                    {
                        continue;
                    }
                    if (P != null)
                    {
                        if (P(element))
                        {
                            yield return element;
                        }
                    }
                    else if (element != null)
                    {
                        yield return element;
                    }
                }
            }
        }
    }

    public int GetAllMapRolesCount(LegacyNativeMapObjectType typ, Predicate<ILegacyNativeMapObj> P = null)
    {
        return GetAllMapRoles(typ, P).Count();
    }

    public T GetMapObject<T>(LegacyNativeMapObjectType typ, uint UID, Predicate<ILegacyNativeMapObj> P = null)
    {
        foreach (ILegacyNativeMapObj allMapRole in GetAllMapRoles(typ, P))
        {
            if (allMapRole.UID == UID)
            {
                return (T)allMapRole;
            }
        }
        return default(T);
    }

    public bool MapContain(LegacyNativeMapObjectType typ, uint UID, Predicate<ILegacyNativeMapObj> P = null)
    {
        foreach (ILegacyNativeMapObj allMapRole in GetAllMapRoles(typ, P))
        {
            if (allMapRole.UID == UID)
            {
                return true;
            }
        }
        return false;
    }

    public void ClearMap(LegacyNativeMapObjectType typ)
    {
        for (int i = 0; i < GetWidthOfBlock(); i++)
        {
            for (int j = 0; j < GetHeightOfBlock(); j++)
            {
                m_setBlock[i, j].Clear(typ);
            }
        }
    }

    public bool TryGetObject<T>(uint UID, LegacyNativeMapObjectType typ, int X, int Y, out T obj)
        where T : ILegacyNativeMapObj
    {
        for (int i = Math.Max(Block(X) - 1, 0); i <= Block(X) + 1 && i < GetWidthOfBlock(); i++)
        {
            for (int j = Math.Max(Block(Y) - 1, 0); j <= Block(Y) + 1 && j < GetHeightOfBlock(); j++)
            {
                LegacyNativeViewPtr viewPtr = m_setBlock[i, j];
                if (viewPtr.TryGetObject<T>(typ, UID, out obj))
                {
                    return true;
                }
            }
        }
        obj = default(T);
        return false;
    }

    public bool Contain(uint UID, int X, int Y)
    {
        for (int i = Math.Max(Block(X) - 1, 0); i <= Block(X) + 1 && i < GetWidthOfBlock(); i++)
        {
            for (int j = Math.Max(Block(Y) - 1, 0); j <= Block(Y) + 1 && j < GetHeightOfBlock(); j++)
            {
                LegacyNativeViewPtr viewPtr = m_setBlock[i, j];
                for (int k = 0; k < 7; k++)
                {
                    if (viewPtr.ContainObject((LegacyNativeMapObjectType)k, UID))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }
}
