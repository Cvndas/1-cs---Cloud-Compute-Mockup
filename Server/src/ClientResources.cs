using System.Net.Sockets;
record ClientResources(
    TcpClient client,
    NetworkStream stream
);