namespace WolfConquer.Core.World;

public sealed partial class LegacyPlayerSpawnPacket
{
    private static bool HasRequiredFields(IReadOnlyDictionary<int, ulong> numericFields)
    {
        foreach (var fieldNumber in RequiredNumericFields)
        {
            if (!numericFields.ContainsKey(fieldNumber))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsKnownNumericField(int fieldNumber)
    {
        return IsRequiredNumericField(fieldNumber) || IsOptionalNumericField(fieldNumber);
    }

    private static bool IsRequiredNumericField(int fieldNumber)
    {
        foreach (var requiredField in RequiredNumericFields)
        {
            if (fieldNumber == requiredField)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOptionalNumericField(int fieldNumber)
    {
        foreach (var optionalField in OptionalNumericFields)
        {
            if (fieldNumber == optionalField)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBoolField(int fieldNumber)
    {
        foreach (var boolField in BoolFields)
        {
            if (fieldNumber == boolField)
            {
                return true;
            }
        }

        return false;
    }

    private static ulong CheckedUInt64(long value, string name)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy player spawn field cannot be negative.");
        }

        return (ulong)value;
    }
}
