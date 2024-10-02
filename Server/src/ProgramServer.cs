global using static System.Console; // Enables use of WriteLine rather than Write
class Program
{
    static Program()
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
            // Object via which the main thread will listen for incoming connections
            Listener listenerInstance = Listener.Instance;
            CloudManager cloudManagerInstance = CloudManager.Instance;
            ChatManager chatManagerInstance = ChatManager.Instance;

            cloudManagerInstance.CreateCloudEmployeePool();
        }
        catch (Exception e) {
            Error.WriteLine("Unexpected Exception: " + e.Message);
        }

        return;
    }

#if DEBUG
    public static void CloudAssert(bool statement)
    {
        System.Diagnostics.Debug.Assert(statement);
    }
#endif
}