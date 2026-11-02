using System.Text.Json;
using WolfConquer.Core.Client;

internal static partial class Program
{
    private static int InspectChinese7937AdditionResolve(string[] args)
    {
        if (args.Length != 6 || args[1] != "7937" ||
            !TryXuanbaoSha256(args[3], out var infoPin) || !TryXuanbaoSha256(args[5], out var additionPin))
            return AdditionResolveRefusal("Arguments");
        try
        {
            if (!Chinese7937DecodedXuanbaoInfo.TryLoadResolveAndAdditionFiles(args[2], args[4], 7937, infoPin, additionPin,
                out var resolve, out var addition, out var failure) || resolve is null || addition is null)
                return AdditionResolveRefusal(failure);
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                FormatVersion = 1, ClientVersion = 7937, Status = "parsed",
                ResolveFieldCount = resolve.Fields.Length, AdditionRowCount = addition.Rows.Count, AdditionNumericColumnCount = 5,
                PlainInfoSha256 = resolve.ContentSha256, PackedAdditionSha256 = addition.PackedSha256,
                DecodedAdditionSha256 = addition.DecodedSha256,
                RuntimeBinding = false, GameplayActivated = false, AdditionMeaningsEstablished = false,
                ResolveBranchOwnerEstablished = false, CurrentActionEstablished = false, RewardOwnershipEstablished = false,
                InputsWritten = false, LuaExecuted = false, NativeClientExecuted = false,
            }));
            return 0;
        }
        catch (Exception)
        {
            // Parser/filesystem exceptions can contain private paths or text.
            // Report no message, input value, partial snapshot or stack trace.
            return AdditionResolveRefusal("InspectionFailed");
        }
    }

    private static int AdditionResolveRefusal(string failure)
    {
        Console.Error.WriteLine(JsonSerializer.Serialize(new
        {
            FormatVersion = 1, ClientVersion = 7937, Status = "refused", Failure = failure,
            RuntimeBinding = false, GameplayActivated = false, AdditionMeaningsEstablished = false,
            ResolveBranchOwnerEstablished = false, CurrentActionEstablished = false, RewardOwnershipEstablished = false,
            InputsWritten = false, LuaExecuted = false, NativeClientExecuted = false,
        }));
        return 1;
    }
}
