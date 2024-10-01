using System.Net;

class ServerAddressInfo
{
    public readonly IPEndPoint ServerIpEndpoint;
    public readonly IPAddress ServerIpAddress;
    public readonly int ServerPortNumber;

    public ServerAddressInfo()
    {
        ServerIpAddress = IPAddress.Loopback;
        ServerPortNumber = 37000;
        ServerIpEndpoint = new IPEndPoint(ServerIpAddress, ServerPortNumber);
    }
}