using System.ComponentModel;
using System.Net;
using System.Runtime.InteropServices;

namespace WolfConquer.Tests;

// Deterministic controls for the native table protocol, not live TCP evidence.
public sealed class DataToolWindowsTcpOwnerTableReaderTests
{
    private sealed record Reply(
        int Result,
        int Length,
        int? RowCount = null,
        NativeMethods.MibTcpRowOwnerPid[]? Rows = null,
        Exception? Error = null);

    private sealed class NativeQuery
    {
        private readonly Queue<Reply> replies;
        internal List<(bool SizeQuery, int Capacity)> Calls { get; } = new();

        internal NativeQuery(params Reply[] replies) => this.replies = new Queue<Reply>(replies);

        internal int Read(IntPtr buffer, ref int length, bool sort, int ipVersion, int tableClass, int reserved)
        {
            Assert.True(sort); Assert.Equal(2, ipVersion); Assert.Equal(5, tableClass); Assert.Equal(0, reserved);
            Calls.Add((buffer == IntPtr.Zero, length));
            Assert.NotEmpty(replies);
            var reply = replies.Dequeue();
            if (reply.Error is not null) throw reply.Error;
            length = reply.Length;
            if (buffer != IntPtr.Zero && reply.RowCount is int count)
                Marshal.WriteInt32(buffer, count);
            if (buffer != IntPtr.Zero && reply.Rows is not null)
            {
                int size = Marshal.SizeOf<NativeMethods.MibTcpRowOwnerPid>();
                for (int i = 0; i < reply.Rows.Length; i++)
                    Marshal.StructureToPtr(reply.Rows[i], IntPtr.Add(buffer, sizeof(int) + i * size), false);
            }
            return reply.Result;
        }
    }

    private static NativeMethods.MibTcpRowOwnerPid Row(uint pid = 42, uint state = 5, int local = 5818, int remote = 80) => new()
    {
        OwningPid = pid, State = state, LocalAddr = 0x0100007F, RemoteAddr = 0x04030201,
        LocalPort = (ushort)IPAddress.HostToNetworkOrder((short)local),
        RemotePort = (ushort)IPAddress.HostToNetworkOrder((short)remote)
    };

    [Fact]
    public void DecodesNativeRowsInOriginalOrderWithoutSortingOrDroppingUnknownStates()
    {
        Assert.Equal(24, Marshal.SizeOf<NativeMethods.MibTcpRowOwnerPid>());
        var query = new NativeQuery(
            new Reply(122, 52),
            new Reply(0, 52, 2, new[] { Row(42), Row(7, 999, 65535, 0) }));
        var rows = global::Program.ReadWindowsIPv4TcpConnections(query.Read);
        Assert.Equal(2, rows.Count);
        Assert.Equal(new WorldRelayCaptureTcpConnection(42, "127.0.0.1", 5818, "1.2.3.4", 80, "Established"), rows[0]);
        Assert.Equal(new WorldRelayCaptureTcpConnection(7, "127.0.0.1", 65535, "1.2.3.4", 0, "Unknown(999)"), rows[1]);
        Assert.Equal(new[] { (true, 0), (false, 52) }, query.Calls);
    }

    [Fact]
    public void RetriesInsufficientBufferWithTheNewRequiredSizeThenReturnsRealDecodedRows()
    {
        var query = new NativeQuery(new Reply(122, 28), new Reply(122, 52), new Reply(0, 52, 2, new[] { Row(1), Row(2) }));
        var rows = global::Program.ReadWindowsIPv4TcpConnections(query.Read);
        Assert.Equal(new[] { 1, 2 }, rows.Select(row => row.OwningProcessId));
        Assert.Equal(new[] { (true, 0), (false, 28), (false, 52) }, query.Calls);
    }

    [Fact]
    public void AllowsThreeGrowingRetriesAndARealFourthRead()
    {
        var query = new NativeQuery(new Reply(122, 28), new Reply(122, 52), new Reply(122, 76), new Reply(122, 100),
            new Reply(0, 100, 4, new[] { Row(1), Row(2), Row(3), Row(4) }));
        var rows = global::Program.ReadWindowsIPv4TcpConnections(query.Read);
        Assert.Equal(new[] { 1, 2, 3, 4 }, rows.Select(row => row.OwningProcessId));
        Assert.Equal(new[] { (true, 0), (false, 28), (false, 52), (false, 76), (false, 100) }, query.Calls);
    }

    [Fact]
    public void ContinuousGrowthFailsAfterFourReadsRatherThanReturningEmptyOrLooping()
    {
        var query = new NativeQuery(new Reply(122, 28), new Reply(122, 52), new Reply(122, 76), new Reply(122, 100), new Reply(122, 124));
        var error = Assert.Throws<Win32Exception>(() => global::Program.ReadWindowsIPv4TcpConnections(query.Read));
        Assert.Equal(122, error.NativeErrorCode);
        Assert.Contains("4 bounded read attempts", error.Message);
        Assert.Equal(5, query.Calls.Count);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(28)]
    public void RejectsInsufficientBufferWithoutStrictSizeProgress(int nextSize)
    {
        var query = new NativeQuery(new Reply(122, 28), new Reply(122, nextSize));
        var error = Assert.Throws<Win32Exception>(() => global::Program.ReadWindowsIPv4TcpConnections(query.Read));
        Assert.Equal(122, error.NativeErrorCode);
        Assert.Contains("without increasing", error.Message);
        Assert.Equal(2, query.Calls.Count);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(50)]
    [InlineData(87)]
    public void PropagatesInitialNativeErrorWithoutAllocatingOrPretendingNoConnections(int errorCode)
    {
        var query = new NativeQuery(new Reply(errorCode, 28));
        var error = Assert.Throws<Win32Exception>(() => global::Program.ReadWindowsIPv4TcpConnections(query.Read));
        Assert.Equal(errorCode, error.NativeErrorCode); Assert.Single(query.Calls);
        Assert.True(query.Calls[0].SizeQuery);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(50)]
    [InlineData(87)]
    [InlineData(234)]
    public void PropagatesFillNativeErrorAndDoesNotRetryUnrelatedErrors(int errorCode)
    {
        var query = new NativeQuery(new Reply(122, 28), new Reply(errorCode, 28));
        var error = Assert.Throws<Win32Exception>(() => global::Program.ReadWindowsIPv4TcpConnections(query.Read));
        Assert.Equal(errorCode, error.NativeErrorCode); Assert.Equal(2, query.Calls.Count);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(67108865)]
    [InlineData(int.MaxValue)]
    public void RejectsInvalidInitialLengthBeforeBufferAllocation(int length)
    {
        var query = new NativeQuery(new Reply(122, length));
        var error = Assert.Throws<Win32Exception>(() => global::Program.ReadWindowsIPv4TcpConnections(query.Read));
        Assert.Equal(13, error.NativeErrorCode); Assert.Single(query.Calls);
        Assert.Contains("buffer bounds", error.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(29)]
    public void RejectsSuccessLengthOutsideAllocatedBufferBeforeReadingRows(int length)
    {
        var query = new NativeQuery(new Reply(122, 28), new Reply(0, length));
        var error = Assert.Throws<Win32Exception>(() => global::Program.ReadWindowsIPv4TcpConnections(query.Read));
        Assert.Equal(13, error.NativeErrorCode); Assert.Equal(2, query.Calls.Count);
        Assert.Contains("allocated buffer", error.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void RejectsInvalidRowCountWithoutReadingPastTheNativeBuffer(int count)
    {
        var query = new NativeQuery(new Reply(122, 28), new Reply(0, 28, count));
        var error = Assert.Throws<Win32Exception>(() => global::Program.ReadWindowsIPv4TcpConnections(query.Read));
        Assert.Equal(13, error.NativeErrorCode);
        Assert.Contains("row count", error.Message);
    }

    [Fact]
    public void RowCountIsBoundedByReturnedLengthNotLargerAllocationCapacity()
    {
        var query = new NativeQuery(new Reply(122, 52), new Reply(0, 28, 2));
        var error = Assert.Throws<Win32Exception>(() => global::Program.ReadWindowsIPv4TcpConnections(query.Read));
        Assert.Equal(13, error.NativeErrorCode);
        Assert.Contains("row count", error.Message);
    }

    [Fact]
    public void RejectsOversizedGrowingRequestBeforeAnotherAllocation()
    {
        var query = new NativeQuery(new Reply(122, 28), new Reply(122, 67108865));
        var error = Assert.Throws<Win32Exception>(() => global::Program.ReadWindowsIPv4TcpConnections(query.Read));
        Assert.Equal(13, error.NativeErrorCode); Assert.Equal(2, query.Calls.Count);
    }

    [Fact]
    public void LaterNativeFailureAfterGrowthIsStillReportedAsItsOriginalError()
    {
        var query = new NativeQuery(new Reply(122, 28), new Reply(122, 52), new(5, 52));
        var error = Assert.Throws<Win32Exception>(() => global::Program.ReadWindowsIPv4TcpConnections(query.Read));
        Assert.Equal(5, error.NativeErrorCode); Assert.Equal(3, query.Calls.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(122)]
    public void AcceptsGenuineValidatedZeroRowTableWithBothLegalSizingResults(int sizingResult)
    {
        var query = new NativeQuery(new Reply(sizingResult, 4), new Reply(0, 4, 0));
        var rows = global::Program.ReadWindowsIPv4TcpConnections(query.Read);
        Assert.Empty(rows);
        Assert.Equal(new[] { (true, 0), (false, 4) }, query.Calls);
    }

    [Fact]
    public void DoesNotSwallowUnexpectedReaderExceptionAfterAllocation()
    {
        var fault = new IOException("owned reader fault");
        var query = new NativeQuery(new Reply(122, 28), new Reply(0, 28, Error: fault));
        Assert.Same(fault, Assert.Throws<IOException>(() => global::Program.ReadWindowsIPv4TcpConnections(query.Read)));
        Assert.Equal(2, query.Calls.Count);
    }

    [Fact]
    public void RejectsNullProviderBeforeNativeCalls()
    {
        Assert.Throws<ArgumentNullException>(() => global::Program.ReadWindowsIPv4TcpConnections(null!));
    }
}
