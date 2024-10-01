using System.Net;

class ClientConnectionInfo
{
    // -------- Public Variables --------- //
    public readonly IPEndPoint ServerIpEndpoint;
    // ----------------------------------- // 

    // -------- Public Methods ----------- //
    public ClientConnectionInfo()
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