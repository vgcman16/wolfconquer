namespace WolfConquer.Tests;

public sealed partial class Chinese7937ItemAlignmentCommandTests
{
    [Theory]
    [InlineData(false, "ReferenceParser")] [InlineData(true, "CurrentParser")]
    public async Task CompleteCanonicalDuplicatesRefuseWithoutLeakingTheirIdentity(bool currentDuplicate, string expected)
    {
        var old = ClientItemCatalog7937AlignmentTests.Row(ClientItemCatalog7937AlignmentTests.Fields("123456789", 65, "secret-field-value"));
        var oldDuplicate = ClientItemCatalog7937AlignmentTests.Row(ClientItemCatalog7937AlignmentTests.Fields("0123456789", 65));
        var current = ClientItemCatalog7937AlignmentTests.Row(ClientItemCatalog7937AlignmentTests.Fields("123456789", 67, "secret-field-value"));
        var currentDuplicateBytes = ClientItemCatalog7937AlignmentTests.Row(ClientItemCatalog7937AlignmentTests.Fields("0123456789", 67));
        var inputs = await WriteInputs(new(currentDuplicate ? old : [.. old, .. oldDuplicate],
            currentDuplicate ? [.. current, .. currentDuplicateBytes] : current));
        using var json = ReadResult(await Inspect(inputs), success: false);
        Assert.Equal(expected, json.RootElement.GetProperty("Failure").GetString());
        Assert.DoesNotContain("123456789", json.RootElement.GetProperty("Failure").GetString()!);
        await Preserved(inputs);
    }

    [Theory]
    [InlineData("pin")] [InlineData("version")] [InlineData("pin-format")] [InlineData("extra")]
    public async Task ClosedVersionPinsAndArgumentsRefuseWithoutWrites(string scenario)
    {
        var inputs = await WriteInputs(ClientItemCatalog7937AlignmentTests.UniquePair());
        string[] args = [Command, scenario == "version" ? "7935" : "7937", inputs.Reference,
            scenario == "pin" ? new string('0', 64) : scenario == "pin-format" ? "zz" : inputs.ReferencePin, inputs.Current, inputs.CurrentPin];
        if (scenario == "extra") args = [.. args, "--output-new", Path.Combine(root, "must-not-exist")];
        using var json = ReadResult(await Run(args), success: false);
        Assert.Equal(scenario == "pin" ? "PinMismatch" : "Arguments", json.RootElement.GetProperty("Failure").GetString());
        await Preserved(inputs);
    }

    [Theory]
    [InlineData("relative.decoded")] [InlineData("\\\\server\\share\\private.decoded")]
    [InlineData("\\\\?\\C:\\private.decoded")]
    public async Task NonordinaryPathSpellingsRefuseWithoutDisclosingTheInput(string path)
    {
        var inputs = await WriteInputs(ClientItemCatalog7937AlignmentTests.UniquePair());
        using var json = ReadResult(await Run(Command, "7937", path, inputs.ReferencePin, inputs.Current, inputs.CurrentPin), success: false);
        Assert.Equal("InputRead", json.RootElement.GetProperty("Failure").GetString());
        Assert.DoesNotContain(path, json.RootElement.GetProperty("Failure").GetString()!);
        await Preserved(inputs);
    }

    [Fact]
    public async Task CompleteWrongWidthAndLateMalformedCurrentTailRefuseRatherThanUseAPrefix()
    {
        var bytes = ClientItemCatalog7937AlignmentTests.UniquePair();
        var inputs = await WriteInputs(new(bytes.Reference, [.. bytes.Current, 0]));
        using var malformed = ReadResult(await Inspect(inputs), success: false);
        Assert.Equal("CurrentParser", malformed.RootElement.GetProperty("Failure").GetString());
        await Preserved(inputs);
        await File.WriteAllBytesAsync(inputs.Current, inputs.ReferenceBytes);
        var replaced = inputs with { CurrentBytes = inputs.ReferenceBytes, CurrentPin = inputs.ReferencePin };
        using var wrongWidth = ReadResult(await Inspect(replaced), success: false);
        Assert.Equal("CurrentParser", wrongWidth.RootElement.GetProperty("Failure").GetString());
        await Preserved(replaced);
    }

    [Fact]
    public async Task MissingAndOversizeOrdinaryInputsFailBeforeParsingOrAnyOutputFile()
    {
        var inputs = await WriteInputs(ClientItemCatalog7937AlignmentTests.UniquePair());
        using var missing = ReadResult(await Run(Command, "7937", Path.Combine(root, "missing.decoded"), inputs.ReferencePin,
            inputs.Current, inputs.CurrentPin), success: false);
        Assert.Equal("InputRead", missing.RootElement.GetProperty("Failure").GetString());
        await Preserved(inputs);
        var oversized = Path.Combine(root, "oversized.decoded");
        using (var lease = new FileStream(oversized, FileMode.CreateNew, FileAccess.Write)) lease.SetLength(64L * 1024 * 1024 + 1);
        using var tooLarge = ReadResult(await Run(Command, "7937", oversized, inputs.ReferencePin, inputs.Current, inputs.CurrentPin), success: false);
        Assert.Equal("InputRead", tooLarge.RootElement.GetProperty("Failure").GetString());
        Assert.Equal(64L * 1024 * 1024 + 1, new FileInfo(oversized).Length);
        Assert.Equal(3, Directory.GetFiles(root).Length); Assert.Empty(Directory.GetDirectories(root));
    }
}
