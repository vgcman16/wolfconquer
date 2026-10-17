using System.Buffers;
using System.Diagnostics;
using System.Net;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class ConquerProtocolProfileTests
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(30);

    public static TheoryData<string> ProfiledServerProjects => new()
    {
        "WolfConquer.AccountServer",
        "WolfConquer.WorldServer",
    };

    [Theory]
    [InlineData("RootD11_7899", ConquerProtocolProfile.RootD11_7899)]
    [InlineData("VisibleDx8D13", ConquerProtocolProfile.VisibleDx8D13)]
    [InlineData(" rootd11_7899 ", ConquerProtocolProfile.RootD11_7899)]
    [InlineData("visibledx8d13", ConquerProtocolProfile.VisibleDx8D13)]
    public void ParseAcceptsOnlyNamedProfiles(
        string value,
        ConquerProtocolProfile expected)
    {
        Assert.Equal(expected, ConquerProtocolProfileConfiguration.Parse(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void ParseRejectsMissingProfile(string? value)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ConquerProtocolProfileConfiguration.Parse(value));

        Assert.Contains(ConquerProtocolProfileConfiguration.EnvironmentVariable, exception.Message);
        Assert.Contains(nameof(ConquerProtocolProfile.RootD11_7899), exception.Message);
        Assert.Contains(nameof(ConquerProtocolProfile.VisibleDx8D13), exception.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("D13")]
    [InlineData("Latest")]
    public void ParseRejectsUnknownProfile(string value)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => ConquerProtocolProfileConfiguration.Parse(value));

        Assert.Contains($"Unknown {ConquerProtocolProfileConfiguration.EnvironmentVariable} value", exception.Message);
        Assert.Contains(value, exception.Message);
    }

    [Fact]
    public void WorldHandlerRejectsUnknownProfile()
    {
        var profile = (ConquerProtocolProfile)int.MaxValue;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new WorldServerFrameHandler(snapshotLoader: null, protocolProfile: profile));

        Assert.Equal("protocolProfile", exception.ParamName);
        Assert.Contains("Unknown Conquer protocol profile.", exception.Message);
    }

    [Theory]
    [MemberData(nameof(ProfiledServerProjects))]
    public async Task ServerRejectsMissingProfileBeforeListening(string projectName)
    {
        var result = await RunServerAsync(projectName, profile: null, listen: true);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(ConquerProtocolProfileConfiguration.EnvironmentVariable, result.StandardError);
        Assert.DoesNotContain("Listening on ", result.StandardOutput, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ProfiledServerProjects))]
    public async Task ServerRejectsUnknownProfileBeforeListening(string projectName)
    {
        var result = await RunServerAsync(projectName, profile: "UnmappedClient", listen: true);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            $"Unknown {ConquerProtocolProfileConfiguration.EnvironmentVariable} value 'UnmappedClient'",
            result.StandardError);
        Assert.DoesNotContain("Listening on ", result.StandardOutput, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ProfiledServerProjects))]
    public async Task ServerAcceptsVisibleDx8D13Profile(string projectName)
    {
        var result = await RunServerAsync(
            projectName,
            nameof(ConquerProtocolProfile.VisibleDx8D13),
            listen: false);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Protocol profile: VisibleDx8D13", result.StandardOutput);
        Assert.Contains("Pass --listen to start accepting TCP clients.", result.StandardOutput);
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError), result.StandardError);
    }

    [Fact]
    public async Task VisibleDx8D13RejectsOpcode2427BeforeRootHandlerAndDisconnects()
    {
        using var log = new StringWriter();
        var disconnectCount = 0;
        var connection = CreateConnection(() => disconnectCount++);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13,
            log: log);

        await handler.HandleFrameAsync(connection, CreateOpcode2427Frame());

        Assert.Equal(1, disconnectCount);
        var output = log.ToString();
        Assert.Contains("PROTOCOL-COLLISION rejected world packet", output);
        Assert.Contains("profile=VisibleDx8D13 opcode=2427", output);
        Assert.Contains("rootHandler=RootD11_7899.MsgItemOperation action=disconnect", output);
        Assert.DoesNotContain("WIRE-EVIDENCE MsgItemOperationRaw", output);
    }

    [Fact]
    public async Task RootD11ProfileRetainsOpcode2427Dispatch()
    {
        using var log = new StringWriter();
        var disconnectCount = 0;
        var connection = CreateConnection(() => disconnectCount++);
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            protocolProfile: ConquerProtocolProfile.RootD11_7899,
            log: log);

        await handler.HandleFrameAsync(connection, CreateOpcode2427Frame());

        Assert.Equal(0, disconnectCount);
        var output = log.ToString();
        Assert.Contains("WIRE-EVIDENCE MsgItemOperationRaw opcode=2427", output);
        Assert.DoesNotContain("PROTOCOL-COLLISION", output);
    }

    private static ConquerTcpConnection CreateConnection(Action disconnect)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            disconnectAsync: _ =>
            {
                disconnect();
                return ValueTask.CompletedTask;
            });
    }

    private static PacketFrame CreateOpcode2427Frame()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.MsgItemOperation, payloadLength: 1);
        writer.Payload[0] = 0;
        var sequence = new ReadOnlySequence<byte>(writer.ToArray());
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static async Task<ProcessResult> RunServerAsync(
        string projectName,
        string? profile,
        bool listen)
    {
        var repositoryRoot = FindRepositoryRoot();
        using var process = TestProjectProcess.Create(repositoryRoot, projectName);
        process.StartInfo.Environment.Remove(ConquerProtocolProfileConfiguration.EnvironmentVariable);
        process.StartInfo.Environment["WOLFCONQUER_ACCOUNT_MYSQL"] = "";
        process.StartInfo.Environment["WOLFCONQUER_ACCOUNT_EMAIL_MYSQL"] = "";
        if (profile is not null)
        {
            process.StartInfo.Environment[ConquerProtocolProfileConfiguration.EnvironmentVariable] = profile;
        }

        if (listen)
        {
            process.StartInfo.ArgumentList.Add("--listen");
            process.StartInfo.ArgumentList.Add("--address");
            process.StartInfo.ArgumentList.Add("127.0.0.1");
            process.StartInfo.ArgumentList.Add("--port");
            process.StartInfo.ArgumentList.Add("0");
        }

        Assert.True(process.Start(), $"Could not start {projectName}.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(ProcessTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            Assert.Fail(
                $"{projectName} did not reject or exit within {ProcessTimeout}. " +
                $"stdout: {await outputTask} stderr: {await errorTask}");
        }

        return new ProcessResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln from the test output directory.");
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
