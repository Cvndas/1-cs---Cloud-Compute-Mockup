using System.Net;

class ServerConnectionInfo
{
    public readonly IPEndPoint ServerIpEndpoint;
    public readonly IPAddress ServerIpAddress;
    public readonly int ServerPortNumber;

    public ServerConnectionInfo()
    {
        ServerIpAddress = IPAddress.Loopback;
        ServerPortNumber = 37000;
        ServerIpEndpoint = new IPEndPoint(ServerIpAddress, ServerPortNumber);
    }
}