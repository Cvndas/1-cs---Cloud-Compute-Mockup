global using static System.Console;
using CloudStates;


class ProgramClient
{
    static ProgramClient()
    {

    }
    public static void Main()
    {
#if DEBUG
        Console.WriteLine("Welcome (Debug)");
#else
        Console.WriteLine("Welcome (Release)");
#endif
        ClientInstance instance = ClientInstance.Instance;
        CloudAssert(instance != null);
        instance?.RunClient();

        return;
    }
#if DEBUG
    public static void CloudAssert(bool statement){
        System.Diagnostics.Debug.Assert(statement);
    }
#endif

}