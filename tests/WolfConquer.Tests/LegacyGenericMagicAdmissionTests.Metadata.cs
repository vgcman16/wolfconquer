using System.Collections;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicClassGatePreparationTests
{
    // Failure-only seam at the real ItemType.RequiredClass read. It cannot
    // accept a cast, save, create an item, publish a desired value or own a Store.
    private sealed class GenericAdmissionRuneFields : IReadOnlyList<string>
    {
        private readonly string[] fields = Enumerable.Repeat("0", 60).ToArray();
        public GenericAdmissionRuneFields()
        {
            fields[0] = "4030809";
            fields[1] = "MPMaster(+9)";
        }
        public bool Armed { get; set; }
        public int Reads { get; private set; }
        public int Count => fields.Length;
        public string this[int index]
        {
            get
            {
                if (Armed && index == ItemType.RequiredClassFieldIndex)
                {
                    Reads++;
                    throw new FormatException("Owned generic optional-rune metadata fault.");
                }
                return fields[index];
            }
        }
        public IEnumerator<string> GetEnumerator() => ((IEnumerable<string>)fields).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
