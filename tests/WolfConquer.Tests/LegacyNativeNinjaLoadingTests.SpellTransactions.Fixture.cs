using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    private static void SeedSpellTransaction(Scope scope)
    {
        SeedLoadingTransaction(scope);
        var first = scope.Spell(1000, 9);
        first.SoulLevel = 3;
        first.PreviousLevel = 6;
        first.UseSpellSoul = 2;
        first.Type = 12;
        first.Soul = 11;
        first.UnKnow = 73;
        first.PacketStamp = 12345;
        first.ColdTime = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        first.StampSpell = first.ColdTime.AddMinutes(1);
        first.IsSpellWithColdTime = true;
        scope.Spell(2000, 8);
    }

    private static void InvokeSpellTransaction(Scope scope, string operation, LegacyNativePacket stream)
    {
        switch (operation)
        {
            case "add": scope.Spells.Add(stream, 1000, 8, 2, 7, -13); break;
            case "remove": scope.Spells.Remove(1000, stream); break;
            case "reborn": scope.Spells.RebornSpell(stream, 1000); break;
            case "clear": scope.Spells.ClearSpells([1000], stream); break;
            case "update": scope.Spells.Update(stream, new() { ID = 1000, Level = 8, SoulLevel = 2,
                PreviousLevel = 7, Experience = -13, Type = 12, Soul = 11 }); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }
}
