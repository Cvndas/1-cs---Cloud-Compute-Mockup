// The component that listens for incoming TCP connections. Runs on the Main Thread.
using System.Net;
using System.Net.Sockets;

namespace Server.src;
class CloudListener
{
    private CloudListener()
    {
        _serverIsRunning = true;
    }

    public static CloudListener Instance {
        get {
            if (_instance == null) {
                _instance = new CloudListener();
            }
            return _instance;
        }
    }

    // Only public so that SIGINT can be handled. Shouldn't be accessed from anywhere else.
    public TcpListener? tcpListener;

    // Listens for incoming connections. Runs on the main thread. 
    public void RunListener()
    {
        tcpListener = new TcpListener(CloudStates.ServerAddress.SERVER_ENDPOINT);
        tcpListener.Start();
        try {
            while (_serverIsRunning) {
                TcpClient incomingClient = tcpListener.AcceptTcpClient();
                NetworkStream stream = incomingClient.GetStream();
                UserResources newUser = new UserResources(incomingClient, stream);
                CloudManager.Instance.AddToUserQueue(newUser);
            }
        }
        catch (Exception e) {
            Error.WriteLine("Unexpected exception in RunListener: " + e.Message);
        }

        finally {
            tcpListener.Stop();
        }
    }

    private static CloudListener? _instance;
    private bool _serverIsRunning;

}