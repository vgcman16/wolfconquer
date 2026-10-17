using System.Net.Sockets;

namespace WolfConquer.Core.Networking;

public static class NetworkExceptionClassifier
{
    public static bool IsPeerDisconnect(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SocketException socketException &&
                IsPeerDisconnect(socketException.SocketErrorCode))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPeerDisconnect(SocketError socketError)
    {
        return socketError is
            SocketError.ConnectionAborted or
            SocketError.ConnectionReset or
            SocketError.OperationAborted or
            SocketError.Shutdown;
    }
}
