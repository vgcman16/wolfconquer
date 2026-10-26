using WolfConquer.Core.Client;

namespace WolfConquer.Tests;

public sealed partial class ClientConfigDefaultPublicTransformTests
{
    [Theory]
    [InlineData("DDD68FC1A4B817541EBD87B334B0C351")] // Recovered type 2.
    [InlineData("90937005088391207E9B01D3D2C1D800")] // Seven FF bytes.
    [InlineData("3D24C07CDA57ADF73C1B842C977CBA85")] // No delimiter.
    [InlineData("00000000000000000000000000000001")] // Minimal result too short.
    public void RealPublicOperationStillRequiresTheCompleteType1Envelope(string blockHex)
    {
        var encoded = Convert.FromHexString(blockHex);
        var modulus = Convert.FromHexString(ModulusHex);
        var before = encoded.ToArray();

        Assert.Throws<InvalidDataException>(() => ClientConfigDefaultPublicTransform.DecodeBlock(encoded, modulus, 16));
        Assert.Equal(before, encoded);
        Assert.Equal(Convert.FromHexString(ModulusHex), modulus);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InputsAtOrAboveTheModulusRefuseInsteadOfBeingReducedToAnAcceptedMessage(bool greater)
    {
        var modulus = Convert.FromHexString(ModulusHex);
        var encoded = modulus.ToArray();
        if (greater) encoded[^1]++;
        var before = encoded.ToArray();

        Assert.Throws<InvalidDataException>(() => ClientConfigDefaultPublicTransform.DecodeBlock(encoded, modulus, 16));
        Assert.Equal(before, encoded);
        Assert.Equal(Convert.FromHexString(ModulusHex), modulus);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void EmptyOrOversizedEncodedInputsRefuseWithoutChangingTheModulus(int length)
    {
        var encoded = new byte[length];
        var modulus = Convert.FromHexString(ModulusHex);
        Assert.Throws<InvalidDataException>(() => ClientConfigDefaultPublicTransform.DecodeBlock(encoded, modulus, 16));
        Assert.Equal(new byte[length], encoded);
        Assert.Equal(Convert.FromHexString(ModulusHex), modulus);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("zero")]
    [InlineData("short")]
    [InlineData("even")]
    [InlineData("too-wide")]
    public void InvalidDefaultProfileModuliRefuseBeforeAnOutputCanBeReturned(string invalid)
    {
        byte[] modulus = invalid switch
        {
            "empty" => [],
            "zero" => new byte[16],
            "short" => Enumerable.Repeat((byte)0xFF, 10).ToArray(),
            "even" => Convert.FromHexString("FFFFFFFFFFFFFF720000000000001322"),
            _ => Enumerable.Repeat((byte)0xFF, 2049).ToArray(),
        };
        var before = modulus.ToArray();
        var encoded = Convert.FromHexString(BinaryBlockHex);

        Assert.Throws<ArgumentException>(() => ClientConfigDefaultPublicTransform.DecodeBlock(encoded, modulus, 16));
        Assert.Equal(before, modulus);
        Assert.Equal(Convert.FromHexString(BinaryBlockHex), encoded);
    }

    [Fact]
    public void AValidPublicResultCannotExceedTheActualCallerCapacity()
    {
        var encoded = Convert.FromHexString(BinaryBlockHex);
        Assert.Throws<InvalidDataException>(() => ClientConfigDefaultPublicTransform.DecodeBlock(
            encoded, Convert.FromHexString(ModulusHex), 4));
        Assert.Equal(Convert.FromHexString(BinaryBlockHex), encoded);
    }

    [Fact]
    public void NegativeCapacityRefusesWithoutReadingOrChangingAnOutput()
    {
        var encoded = Convert.FromHexString(BinaryBlockHex);
        Assert.Throws<ArgumentOutOfRangeException>(() => ClientConfigDefaultPublicTransform.DecodeBlock(
            encoded, Convert.FromHexString(ModulusHex), -1));
        Assert.Equal(Convert.FromHexString(BinaryBlockHex), encoded);
    }

    [Fact]
    public void ZeroIntegerHasNoValidType1Envelope()
    {
        Assert.Throws<InvalidDataException>(() => ClientConfigDefaultPublicTransform.DecodeBlock(
            new byte[16], Convert.FromHexString(ModulusHex), 16));
    }

    [Theory]
    [InlineData(63)]
    [InlineData(65)]
    public void WordCompositionRejectsIncompleteOrExtraRecipes(int length)
    {
        var words = Enumerable.Repeat(0x12345678u, length).ToArray();
        var before = words.ToArray();
        Assert.Throws<ArgumentException>(() => ClientConfigDefaultPublicTransform.DecodeWordBlock(
            Convert.FromHexString(BinaryBlockHex), words, 16));
        Assert.Equal(before, words);
    }
}
