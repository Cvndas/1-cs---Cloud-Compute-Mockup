using System.Net;

namespace CloudStates;
public class ServerAddress
{
    public const int SERVER_PORT = 37000;
    public static readonly IPAddress SERVER_IP = IPAddress.Loopback;
    public static readonly IPEndPoint SERVER_ENDPOINT = new IPEndPoint(SERVER_IP, SERVER_PORT);
}