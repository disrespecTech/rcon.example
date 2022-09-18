namespace rcon.library;

using System;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

public class SocketClient : IDisposable
{
    public static short TERMINATION_SHORT = 0;
    public static byte[] TERMINATION_BYTE = new byte[1];

    public bool IsConnect => socket?.Connected ?? false;
    public bool IsDisposed { get; private set; }

    private Socket? socket;
    private NetworkStream? networkStream;
    private Thread backgroundThread;

    private byte[] payload = new byte[1024];
    private byte[] dispatchCache = Array.Empty<byte>();

    public event EventHandler<byte[]> PacketReceived;

    public SocketClient()
    {
        IsDisposed = false;
    }

    public async Task<bool> Connect(string hostname, int port, CancellationToken cancellation)
    {
        var endpoint = IPEndPoint.Parse($"{hostname}:{port}");

        socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        socket.Connect(endpoint);

        //await tcpClient.ConnectAsync(hostname, port, cancellation);
        if (socket == null || !socket.Connected) return false;

        // Create local network stream
        networkStream = new NetworkStream(socket);
        return true;
    }

    public void Dispose()
    {
        IsDisposed = true;

        // TODO test correct dispose chain
        socket?.Dispose();
        networkStream?.Dispose();
        socket?.Dispose();
    }

    public void Write(int number)
    {
        //Send(BitConverter.GetBytes(number).Reverse().ToArray());
        Send(BitConverter.GetBytes(number));
    }

    public void Write(string data)
    {
        Send(Encoding.ASCII.GetBytes(data));

        // Rcon uses null terminated ascii string see: https://developer.valvesoftware.com/wiki/Source_RCON_Protocol#Packet_Body
        if (data[^1] != 0) SendTermination();
    }

    public void SendTermination()
    {
        // Rcon packets end with a null string see: https://developer.valvesoftware.com/wiki/Source_RCON_Protocol#Empty_String
        Send(TERMINATION_BYTE);
    }

    private void Send(byte[] data)
    {
        var currentLength = dispatchCache.Length;
        Array.Resize(ref dispatchCache, currentLength + data.Length);
        Array.Copy(data, 0, dispatchCache, currentLength, data.Length);
    }

    public void Complete()
    {
        if (networkStream == null) throw new InvalidOperationException("Socket not ready");

        Console.WriteLine(Convert.ToHexString(dispatchCache));
        networkStream.Write(dispatchCache, 0, dispatchCache.Length);
        dispatchCache = Array.Empty<byte>();
    }

    public void StartListening()
    {
        // TODO def not optimal but quick and dirty
        backgroundThread = new Thread(async () =>
        {
            while (!IsDisposed)
            {
                await Bind();
            }
        })
        { IsBackground = true };

        backgroundThread.Start();
    }

    private async Task Bind()
    {
        if (networkStream == null) throw new InvalidOperationException("Socket not ready");

        var length = await networkStream.ReadAsync(payload, 0, payload.Length);
        if (length > 0)
        {
            // It's possible that the payload byte[] isn't full of packet data so we copy only what valid data is contained within the payload
            PacketReceived?.Invoke(networkStream, payload[..length]);
        }
    }
}

