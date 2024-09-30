#define SERVER_LOGGING

class Connection
{
    private string? _username;

    // TODO : Instead of saving Text Files, save PNG images. SixLabors.ImageSharp via NuGet
    private String[]? TextFiles;

    public Connection()
    {

    }

}

class Server
{
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
            SetupServer();
            CloudCastle.MergeSort.Sort();
        }
        catch (Exception e) {
            Console.WriteLine("Exception caught in SetupServer(): " + e.Message);
        }


        // ---------- DEBUG LOGGING ------------  //
#if SERVER_LOGGING
        _logFile?.Close();
#endif
        // ------------------------------------- //
        Console.WriteLine("Closing Server!");
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


    private static void SetupServer()
    {

    }
}
