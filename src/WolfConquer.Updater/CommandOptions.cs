namespace WolfConquer.Updater;

internal sealed class CommandOptions
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);
    internal CommandOptions(string[] args, string[] flags, string[] values)
    {
        for (int index = 0; index < args.Length; index++)
        {
            var name = args[index];
            string? value = null;
            if (values.Contains(name, StringComparer.Ordinal))
            {
                if (++index == args.Length || args[index].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException("Missing option value: " + name);
                value = args[index];
            }
            else if (!flags.Contains(name, StringComparer.Ordinal)) throw new ArgumentException("Unknown option: " + name);
            if (!_values.TryAdd(name, value)) throw new ArgumentException("Duplicate option: " + name);
        }
    }
    internal bool Has(string name) => _values.ContainsKey(name);
    internal string? Optional(string name) => _values.GetValueOrDefault(name);
    internal string Required(string name) => Optional(name) ?? throw new ArgumentException("Required option: " + name);
    internal int Number(string name, int fallback, int minimum, int maximum) => Optional(name) is not { } text ? fallback :
        int.TryParse(text, out int value) && value >= minimum && value <= maximum ? value :
        throw new ArgumentException("Out-of-range integer option: " + name);
}
