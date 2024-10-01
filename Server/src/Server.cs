// #define SERVER_LOGGING

using System.IO;
using System.Net.Sockets;
using System.Text;

class Server
{
    // The actual main function
    private static async void RunCloudServer()
    {
        Console.WriteLine("Starting up the server");
        ServerConnectionInfo serverConnectionInfo = new();
        TcpListener tcpListener = new(serverConnectionInfo.ServerIpEndpoint);

        try {
            tcpListener.Start();
            using TcpClient handler = await tcpListener.AcceptTcpClientAsync();
            Console.WriteLine("Awaiting a connection...");
            // using TcpClient handler = await acceptConnection;
            await using NetworkStream stream = handler.GetStream();
            Console.WriteLine("Connection made!");

            byte[] buffer = new byte[1_024];
            int received = await stream.ReadAsync(buffer);
            
            Console.WriteLine("Message received from client: " + Encoding.UTF8.GetString(buffer));

            string response = "Hey there, client. How's it going?";
            stream.Write(Encoding.UTF8.GetBytes(response));
            handler.Close();    
        }

        catch (Exception e) {
            Console.WriteLine("Unexpected exception: " + e.Message);
        }
        finally {
            tcpListener.Stop();
        }

        Console.WriteLine("Closing Server!");
        return;
    }


    public static void Main(string[] args)
    {
        // ---------------- DEBUG LOGGING ------------ // 
#if SERVER_LOGGING

        try {
            string currentDirectory = Directory.GetCurrentDirectory();
            if (!currentDirectory.EndsWith("CloudCastle")) {
                Console.WriteLine("When logging, please cd into the root of the solution, and run the program via the runClient.sh script");
                throw new Exception();
            }
            Console.WriteLine("Server logging is enabled.");
            _logFile = new StreamWriter(Path.Combine(Directory.GetCurrentDirectory(), "log_server.txt"));
        }
        catch (Exception e) {
            Console.WriteLine("Error upon opening logFile.txt: " + e.Message);
            _logFile?.Close();
            return;
        }
#endif
        // ------------------------------------------- //

        try {
            RunCloudServer();
        }
        catch (Exception e) {
            Console.WriteLine("Exception caught in SetupServer(): " + e.Message);
        }


        // ---------- DEBUG LOGGING ------------  //
#if SERVER_LOGGING
        _logFile?.Close();
#endif
        // ------------------------------------- //
        Console.WriteLine("Closing program.");
        return;
    }
    // ------------ DEBUG LOGGING -------------- // 
#if SERVER_LOGGING
    private static StreamWriter? _logFile;
    // LL : Write to LogFile 
    public static void LL(string message)
    {
        _logFile?.WriteLine(message);
    }
#endif
    // ----------------------------------------- //


}
