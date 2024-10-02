using System.Net.Sockets;
record UserResources(
    TcpClient client,
    NetworkStream stream
);