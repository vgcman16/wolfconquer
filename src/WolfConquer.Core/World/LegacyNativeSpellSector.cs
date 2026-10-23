using System;

namespace WolfConquer.Core.World;

// Entire native AttackHandler.Algoritms.Sector. Do not substitute the host's
// cleaned-up cone geometry: native angular bounds and sticky addextra differ.
internal class LegacyNativeSpellSector
{
    private int attackerX;
    private int attackerY;
    private int attackX;
    private int attackY;
    private int degree;
    private int sectorsize;
    private int leftside;
    private int rightside;
    private int distance;
    private bool addextra;

    public LegacyNativeSpellSector(int attackerX, int attackerY, int attackX, int attackY)
    {
        this.attackerX = attackerX;
        this.attackerY = attackerY;
        this.attackX = attackX;
        this.attackY = attackY;
        degree = LegacyNativeRoleCore.GetDegree((ushort)attackerX, (ushort)attackX, (ushort)attackerY, (ushort)attackY);
        addextra = false;
    }

    public void Arrange(int sectorsize, int distance)
    {
        this.distance = Math.Min(distance, 14);
        this.sectorsize = sectorsize;
        leftside = degree - sectorsize / 2;
        if (leftside < 0)
        {
            leftside += 360;
        }
        rightside = degree + sectorsize / 2;
        if (leftside < rightside || rightside - leftside != this.sectorsize)
        {
            rightside += 360;
            addextra = true;
        }
    }

    public bool Inside(int X, int Y)
    {
        if (LegacyNativeRoleCore.GetDistance((ushort)X, (ushort)Y, (ushort)attackerX, (ushort)attackerY) <= distance)
        {
            int num = LegacyNativeRoleCore.GetDegree((ushort)attackerX, (ushort)X, (ushort)attackerY, (ushort)Y);
            if (addextra)
            {
                num += 360;
            }
            if (num >= leftside && num <= rightside)
            {
                return true;
            }
        }
        return false;
    }
}
