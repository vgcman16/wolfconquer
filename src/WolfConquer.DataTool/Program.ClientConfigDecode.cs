using System.Security;
using System.Text.Json;

internal static partial class Program
{
    private static int ClientConfigDecodeCommand(string[] args)
    {
        var published = false;
        try
        {
            var options = ParseConfigDecodeOptions(args);
            ValidateConfigDecodeRoles(options);
            var sizes = ExecuteConfigDecode(options, ref published);
            Console.WriteLine($"Profile=d13-default InputBytes={sizes.Input} ResultBytes={sizes.Result} OutputPublished=True");
            Console.WriteLine("Byte output only; native provider selection, text/parser semantics and client loading are not established.");
            return 0;
        }
        catch (ConfigDecodeOperationException failure)
        {
            WriteConfigDecodeFailure(failure.Primary.GetType().Name, published,
                failure.Cleanup is null ? "None" : failure.Cleanup.GetType().Name);
            return 1;
        }
        catch (Exception error) when (IsConfigDecodeFailure(error))
        {
            WriteConfigDecodeFailure(error.GetType().Name, published, "None");
            return 1;
        }
    }

    private static bool IsConfigDecodeFailure(Exception error) => error is IOException or UnauthorizedAccessException or
        ArgumentException or NotSupportedException or OverflowException or JsonException or SecurityException;

    private static void WriteConfigDecodeFailure(string error, bool published, string cleanup)
    {
        // Never echo payloads, words, modulus values, caller paths or exception messages.
        try
        {
            Console.Error.WriteLine($"Client config decode failed: {error} OutputPublished={published} Cleanup={cleanup}.");
            Console.Error.WriteLine(ClientConfigDecodeUsage);
        }
        catch (IOException) { /* Diagnostic failure cannot undo a committed output. */ }
    }
}
