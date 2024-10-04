using System.Net.Sockets;
namespace Server.src;
class UserResources
{
    public UserResources(TcpClient client, NetworkStream stream){
        this.client = client;
        this.stream = stream;
    }
    public TcpClient client;
    public NetworkStream stream;
    public String? username;
}