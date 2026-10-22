#nullable disable

// Native identity ONLY: this does not escape SQL or make interpolation safe.
namespace WolfConquer.Core.Data;

internal static class LegacyNativeMySqlExtensions
{
	public static string MySqlEscape(this string usString)
	{
		return usString;
	}
}
