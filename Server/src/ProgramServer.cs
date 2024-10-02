global using static System.Console; // Enables use of WriteLine rather than Write
global using System.Diagnostics;
class ProgramServer
{
    static ProgramServer()
    {

    }
    public static void Main()
    {
#if DEBUG
        WriteLine("Welcome (Debug)");
#else
        WriteLine("Welcome (Release)");
#endif

        try {
            // Handle Ctrl+C sigint
            Console.CancelKeyPress += new ConsoleCancelEventHandler(myConsoleCancelHandler);


            // Construct the singletons
            CloudListener _listenerInstance = CloudListener.Instance; // Runs on main thread
            ThreadRegistry.ListenerThreadHash = Thread.CurrentThread.GetHashCode();
            CloudManager cloudManagerInstance = CloudManager.Instance; // Launches separate thread
            ChatManager chatManagerInstance = ChatManager.Instance; // TODO CHAT: Launche separate thread.

            cloudManagerInstance.CreateCloudEmployeePool();

            _listenerInstance.RunListener();
        }
        catch (Exception e) {
            Error.WriteLine("Unexpected Exception: " + e.Message);
        }

        return;
    }


    protected static void myConsoleCancelHandler(object? sender, ConsoleCancelEventArgs args)
    {
        Error.WriteLine("\nControl C handled.");
        CloudListener.Instance.tcpListener?.Stop();
        Error.WriteLine("Exiting the program (gracefully).");
        Environment.Exit(0);
    }

}