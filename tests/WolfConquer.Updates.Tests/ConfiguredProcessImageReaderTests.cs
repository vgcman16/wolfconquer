using System.ComponentModel;
using WolfConquer.Updates.Install;

namespace WolfConquer.Updates.Tests;

public sealed class ConfiguredProcessImageReaderTests
{
    [Fact]
    public void AlreadyExitedExactHandleDoesNotReadAnImage()
    {
        var lease = new ObservedLease([true], error: new Win32Exception(5));
        Assert.Null(ConfiguredProcessImageReader.Read(lease));
        Assert.Equal(new[] { "exit" }, lease.Reads);
    }

    [Fact]
    public void AliveAttributedImageReturnsTheExactCanonicalPath()
    {
        string expected = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "wolf-attribution", "client.exe"));
        var lease = new ObservedLease([false], expected);
        Assert.Equal(expected, ConfiguredProcessImageReader.Read(lease));
        Assert.Equal(new[] { "exit", "image" }, lease.Reads);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void ImageReadRaceIsIgnoredOnlyAfterSameLeaseConfirmsExit(int failure)
    {
        var lease = Failure(failure, exited: true);
        Assert.Null(ConfiguredProcessImageReader.Read(lease));
        Assert.Equal(new[] { "exit", "image", "exit" }, lease.Reads);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void AliveUnattributableImageAlwaysRefuses(int failure)
    {
        var lease = Failure(failure, exited: false);
        Assert.ThrowsAny<Exception>(() => ConfiguredProcessImageReader.Read(lease));
        Assert.Equal(new[] { "exit", "image", "exit" }, lease.Reads);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("client.exe")]
    [InlineData("../client.exe")]
    public void MissingOrRelativeImageWhileAliveCannotAuthorizeAnUpdate(string? image)
    {
        var lease = new ObservedLease([false, false], image!);
        Assert.Throws<IOException>(() => ConfiguredProcessImageReader.Read(lease));
        Assert.Equal(new[] { "exit", "image", "exit" }, lease.Reads);
    }

    [Fact]
    public void UnknownInitialExitStatusRefusesWithoutReadingImage()
    {
        var failure = new Win32Exception(5, "exit observation denied");
        var lease = new ObservedLease([], exitError: failure);
        Assert.Same(failure, Assert.Throws<Win32Exception>(() => ConfiguredProcessImageReader.Read(lease)));
        Assert.Equal(new[] { "exit" }, lease.Reads);
    }

    [Fact]
    public void UnknownExitAfterImageFailureRefusesInsteadOfClaimingDisappearance()
    {
        var failure = new Win32Exception(6, "exact handle observation denied");
        var lease = new ObservedLease([false], error: new Win32Exception(299), exitError: failure);
        Assert.Same(failure, Assert.Throws<Win32Exception>(() => ConfiguredProcessImageReader.Read(lease)));
        Assert.Equal(new[] { "exit", "image", "exit" }, lease.Reads);
    }

    [Fact]
    public void UnrelatedProgrammingFailureIsNotSwallowedEvenIfALaterExitWouldBeTrue()
    {
        var failure = new NotSupportedException("unrelated query implementation fault");
        var lease = new ObservedLease([false, true], error: failure);
        Assert.Same(failure, Assert.Throws<NotSupportedException>(() => ConfiguredProcessImageReader.Read(lease)));
        Assert.Equal(new[] { "exit", "image" }, lease.Reads);
    }

    private static ObservedLease Failure(int kind, bool exited) => new([false, exited], error: kind switch
    {
        0 => new Win32Exception(299, "partial-copy race"),
        1 => new Win32Exception(5, "access denied"),
        2 => new IOException("empty image"),
        3 => new InvalidOperationException("process disappeared"),
        _ => new ArgumentException("invalid image path"),
    });

    private sealed class ObservedLease(bool[] exits, string image = "", Exception? error = null,
        Exception? exitError = null) : IConfiguredProcessImageLease
    {
        private int next;
        internal List<string> Reads { get; } = [];
        public bool HasExited()
        {
            Reads.Add("exit");
            if (next < exits.Length) return exits[next++];
            throw exitError ?? new InvalidOperationException("Unexpected extra exit query.");
        }
        public string ReadImage()
        {
            Reads.Add("image");
            if (error is not null) throw error;
            return image;
        }
    }
}
