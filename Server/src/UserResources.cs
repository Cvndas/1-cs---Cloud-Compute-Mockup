using System.Net.Sockets;
using CloudStates;
namespace Server.src;
class UserResources
{
    public UserResources(TcpClient client, NetworkStream stream, CloudSenderReceiver cloudSenderReceiever){
        this.client = client;
        this.stream = stream;
        this.senderReceiver = cloudSenderReceiever;
    }
    public TcpClient client;
    public NetworkStream stream;
    public CloudSenderReceiver senderReceiver;
    public String? username;
}