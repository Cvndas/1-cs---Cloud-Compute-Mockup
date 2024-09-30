#define CLIENT_LOGGING

class Client
{

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
        Console.WriteLine("Connecting you to the server...");

        Console.WriteLine("Connected!");




        // ---------- DEBUG LOGGING ------- //
#if CLIENT_LOGGING
        _logFile?.Close();
#endif
        // ------------------------------------- //

        Console.WriteLine("Closing Client!");
        return;
    }

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