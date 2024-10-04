global using static System.Console; // Enables use of WriteLine rather than Write
global using System.Diagnostics;

namespace Server.src;
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

        var myWriter = new TextWriterTraceListener(Out);
        Trace.Listeners.Add(myWriter);


        try {
            // Handle Ctrl+C sigint
            CancelKeyPress += new ConsoleCancelEventHandler(myConsoleCancelHandler);

            // Construct the singletons
            CloudListener _listenerInstance = CloudListener.Instance; // Runs on main thread
            ThreadRegistry.ListenerThreadId = Environment.CurrentManagedThreadId;
            CloudManager cloudManagerInstance = CloudManager.Instance; // Launches separate thread
            ChatManager chatManagerInstance = ChatManager.Instance; // TODO CHAT: Launche separate thread.

            cloudManagerInstance.CreateCloudEmployeePool();
            chatManagerInstance.CreateChatEmployeePool();

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
        Error.WriteLine("Exited the server (gracefully).");
        Environment.Exit(0);
    }

}