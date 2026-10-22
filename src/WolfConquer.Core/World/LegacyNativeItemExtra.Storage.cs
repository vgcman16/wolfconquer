namespace WolfConquer.Core.World;

// Native message storage/constructor only; no stabilization or add-extra handlers.
internal partial class LegacyNativeItemExtra
{
    public class Artefacts
    {
        public const byte Refinary = 0;
        public const byte Purification = 1;
    }

    public List<Purification> Purifications;
    public List<Refinery> Refinerys;

    public LegacyNativeItemExtra()
    {
        Purifications = new List<Purification>();
        Refinerys = new List<Refinery>();
    }
}
