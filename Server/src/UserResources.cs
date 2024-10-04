using System.Net.Sockets;
namespace Server.src;
record UserResources(
    TcpClient client,
    NetworkStream stream
);