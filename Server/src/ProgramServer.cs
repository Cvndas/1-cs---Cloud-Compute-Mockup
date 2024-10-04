global using static System.Console; // Enables use of WriteLine rather than Write
global using System.Diagnostics;

namespace Server.src;

// Note: An easy way to break or hack this server would be to input characters that are 2 bytes in length, rather than 1.
// The code always assumes that String.Length is the same as byte[].Count
// This should be fixable by going through all the string.Length and replacing it with Encoding.UTF8.GetBytes(string).Count
// But honestly, since this program will never run online, I probably won't fix that.
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