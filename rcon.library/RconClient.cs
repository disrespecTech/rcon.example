namespace rcon.library;
public class RconClient
{
    // See: https://developer.valvesoftware.com/wiki/Source_RCON_Protocol#Packet_Type
    private const int SERVERDATA_AUTH = 3;
    private const int SERVERDATA_AUTH_RESPONSE = 2;
    private const int SERVERDATA_EXECCOMMAND = 2;
    private const int SERVERDATA_RESPONSE_VALUE = 0;
    private const int HEADER_SIZE = 4 * 2;
    private const int TERMINATION_SIZE = 2;

    private byte[] reciveCache = Array.Empty<byte>();
    private SocketClient client;
    private Message? message;

    public event EventHandler<Message> MessageReceived;

    public RconClient(SocketClient client)
    {
        this.client = client;
        this.client.PacketReceived += Client_PacketReceived;
    }

    public void Auth(int id, string password)
    {
        SendCommand(id, SERVERDATA_AUTH, password);
    }

    public void List(int id)
    {
        SendCommand(id, SERVERDATA_EXECCOMMAND, "list");
    }

    public void Tell(int id, string name, string message)
    {
        SendCommand(id, SERVERDATA_EXECCOMMAND, $"tell {name} {message}");
    }

    public void Raw(int id, string command)
    {
        SendCommand(id, SERVERDATA_EXECCOMMAND, command);
    }

    private void SendCommand(int id, int type, string command)
    {
        SendLength(command);

        client.Write(id);
        client.Write(type);
        client.Write(command);
        client.SendTermination();
        client.Complete();
    }

    private void SendLength(string? body = null)
    {
        client.Write(HEADER_SIZE + TERMINATION_SIZE + (body?.Length ?? 0));
    }

    private void Client_PacketReceived(object? sender, byte[] payload)
    {
        // We must join our cache payload (on going parsing) with our new payload (fresh data)
        var cacheStart = reciveCache.Length;
        Array.Resize(ref reciveCache, cacheStart + payload.Length);
        Array.Copy(payload, 0, reciveCache, cacheStart, payload.Length);

        reciveCache = ParsePacket(reciveCache);
    }

    private byte[] ParsePacket(byte[] payload)
    {
        // A packet can NOT be less then 10 bytes long seet: https://developer.valvesoftware.com/wiki/Source_RCON_Protocol#Packet_Size
        if (message == null)
        {
            if (payload.Length < HEADER_SIZE) return payload;

            message = new Message();
            payload = message.ParseHeader(payload);
        }

        // Try parse rcon packet message body
        payload = message.TryParseBody(payload);

        // If successful clear caached message and return remaining payload
        if (message.IsComplete)
        {
            // Not the best solution for async work flow
            MessageReceived?.Invoke(this, message);
            message = null;
        }

        return payload;
    }
}


