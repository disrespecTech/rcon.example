namespace rcon.library;

using System;
using System.Text;

public class Message
{
    public int Id { get; private set; }
    public int Length { get; private set; }
    public int Type { get; private set; }
    public string Body { get; private set; }
    public bool IsComplete { get; private set; }

    private int bodyIndex = 0;

    public Message()
    {
        Body = "";
    }

    public byte[] ParseHeader(byte[] payload)
    {
        if (payload.Length < 4 * 3) throw new Exception("Payload not valid: missing headers");
        Length = BitConverter.ToInt32(payload, 0);
        Id = BitConverter.ToInt32(payload, 4);
        Type = BitConverter.ToInt32(payload, 8);

        // Remove header and return result
        var tmp = new byte[payload.Length - 12];
        Array.Copy(payload, 12, tmp, 0, tmp.Length);
        return tmp;
    }

    /// <summary>
    /// Accepts the payload after the header (id/type) has already been parsed.
    /// Parses the ascii data until it finds 2 null bytes (aka 16 bit zeroed short)
    /// </summary>
    /// <param name="payload"></param>
    /// <returns>Left over unparsed data for next packet</returns>
    public byte[] TryParseBody(byte[] payload)
    {
        for (; bodyIndex < payload.Length - 1; bodyIndex++)
        {
            // A zeroed short is our packet termination signal we can parse the body now and move on to the next packet
            if (BitConverter.ToInt16(payload, bodyIndex) == SocketClient.TERMINATION_SHORT)
            {
                IsComplete = true;
                Body = bodyIndex <= 0 ? string.Empty : Encoding.ASCII.GetString(payload, 0, bodyIndex);

                // because of the nature of streams it's possible during high throughtput that we can have packets against each other in the stream
                // we handle that by finding the termination signal and cutting from that point on, if there is data after it we must not lose it as
                // is it our next packet so we cut the "used" packet data (aka the previous consumed byte[]) and return the left over byte[] as the next
                // packet
                var nextPacketStartIndex = bodyIndex + 2;
                if (nextPacketStartIndex < payload.Length)
                {
                    var tempArray = new byte[payload.Length - nextPacketStartIndex];
                    Array.Copy(payload, nextPacketStartIndex, tempArray, 0, tempArray.Length);
                    return tempArray;
                }
                else
                {
                    // there is no next packet in the payload return nothing
                    return Array.Empty<byte>();
                }
            }
        }

        // We failed to find a complete packet return the whole payload and caller must try again
        return payload;
    }
}

