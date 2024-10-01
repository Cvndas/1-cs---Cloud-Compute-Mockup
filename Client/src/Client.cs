#define CLIENT_LOGGING
#define DEBUG_ASSERTS


using System.Net.Sockets;
using System.Text;

class Client
{
    public static ClientConnectionInfo? connectionData;
    // --------------- Public Methods ------------ // 
    public static void Main()
    {
        // ---------- DEBUG LOGGING ----------- // 
#if CLIENT_LOGGING
        try {
            string currentDirectory = Directory.GetCurrentDirectory();
            if (!currentDirectory.EndsWith("CloudCastle")) {
                Console.WriteLine("When logging, please cd into the root of the solution, and run the program via the runClient.sh script");
                throw new Exception();
            }
            Console.WriteLine("Client logging is enabled.");
            _logFile = new StreamWriter(Path.Combine(Directory.GetCurrentDirectory(), "log_client.txt"));
        }
        catch (Exception e) {
            Console.WriteLine("Error upon opening logFile.txt: " + e.Message);
            _logFile?.Close();
            return;
        }
#endif
        // ------------------------------------- //

        LL("++++++CLIENT LOG+++++++");
        Console.WriteLine("Welcome to the Castle of Clouds!");

        try {
            RunCloudInstance();
        }
        catch (Exception e) {
            Console.WriteLine("Unexpected error in Main(): " + e.Message);
        }


        // ---------- DEBUG LOGGING ------- //
#if CLIENT_LOGGING
        _logFile?.Close();
#endif
        // ------------------------------------- //

        Console.WriteLine("Closing Client!");
        return;
    }
    // ------------------------------------------- // 

    // ------------- Private Members ------------- // 

    // ------------------------------------------- // 

    // ------------- Private Methods ------------- // 

    private static void RunCloudInstance()
    {
        // Simply establish a "hello world" connection, both ways, over TCP.
        Console.WriteLine("Trying to connect to the server...");

        ClientConnectionInfo connectionData = new();
        using TcpClient client = new();
        client.Connect(connectionData.ServerIpEndpoint);

        using NetworkStream stream = client.GetStream();

        // Send Hello World
        string message = "Hello World!";
        var messageBytes = Encoding.UTF8.GetBytes(message);
        stream.Write(messageBytes);

        // Wait for a response
        int bytesToRead = 1024;
        byte[] buffer = new byte[bytesToRead];
        int received = stream.Read(buffer);
        Console.WriteLine("Received: " + received);
        Console.WriteLine("Response from server: " + Encoding.UTF8.GetString(buffer));

        return;
    }
    // ------------------------------------------- //



    // ------------ DEBUG ASSERT --------------- // 
#if DEBUG_ASSERTS
    public static void AA(Boolean statement)
    {
        System.Diagnostics.Debug.Assert(statement);
    }
#endif



    // ------------ DEBUG LOGGING -------------- // 
#if CLIENT_LOGGING
    private static StreamWriter? _logFile;
    // LL : Write to LogFile 
    public static void LL(string message)
    {
        _logFile?.WriteLine(message);
    }
#endif
    // ----------------------------------------- //


}