using System.Net.Sockets;

namespace WolfConquer.Core.Networking;

internal partial class LegacyNativeSecuritySocket
{
    internal enum SendResult { Refused, Queued, CipherStateIndeterminate }

    private bool sendCipherIndeterminate;

    // Actual queue admission only; no Connection send or pump is performed here.
    public void Send(LegacyNativePacket msg) => TrySend(msg);

    internal unsafe SendResult TrySend(LegacyNativePacket? msg, int maximumLength = int.MaxValue)
    {
        try
        {
            if (!Alive)
            {
                return SendResult.Refused;
            }
            lock (locckss)
            {
                if (!Alive) return SendResult.Refused;
                if (sendCipherIndeterminate) return SendResult.CipherStateIndeterminate;
                ArgumentNullException.ThrowIfNull(msg);
                var queue = SendQueue ?? throw new InvalidOperationException("Native send queue is not initialized.");
                byte[] plaintext = msg.CaptureSendBytes(maximumLength);
                var cipher = Crypto;
                byte[] output = cipher is null ? plaintext : new byte[plaintext.Length];
                queue.EnsureCapacity(checked(queue.Count + 1));
                if (cipher is not null)
                {
                    try
                    {
                        fixed (byte* source = plaintext)
                            cipher.Encrypt(source, 0, output, 0, plaintext.Length);
                    }
                    catch (Exception error) when (error is not AccessViolationException)
                    {
                        // Cipher progress cannot be rolled back honestly. Fence this
                        // actual socket permanently; recovery needs a new connection owner.
                        sendCipherIndeterminate = true;
                        return SendResult.CipherStateIndeterminate;
                    }
                }
                queue.Enqueue(output);
                return SendResult.Queued;
            }
        }
        catch (SocketException)
        {
            Disconnect();
            return SendResult.Refused;
        }
        catch (Exception error) when (error is not AccessViolationException)
        {
            return SendResult.Refused;
        }
    }
}
