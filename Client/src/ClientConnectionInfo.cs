using System.Net;

class ClientNetworkInfo
{
    // -------- Public Variables --------- //
    public readonly IPEndPoint ServerIpEndpoint;
    // ----------------------------------- // 

    // -------- Public Methods ----------- //
    public ClientNetworkInfo()
    {
        ServerIpEndpoint = new(SERVER_IP, SERVER_PORT);
    }
    // ----------------------------------- // 

    // -------- Private Variables -------- //
    private const int SERVER_PORT = 37000;
    private readonly IPAddress SERVER_IP = IPAddress.Loopback;
    // ----------------------------------- // 

    // --------- Private Methods --------- // 

    // ----------------------------------- // 
}