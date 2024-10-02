// The component that listens for incoming TCP connections. Runs on the Main Thread.
class Listener
{
    private Listener(){

    }

    public static Listener Instance
    {
        get {
            if (_instance == null){
                _instance = new Listener();
            }
            return _instance;
        }
    }

    public void RunListener(){

    }
    private static Listener? _instance;

}