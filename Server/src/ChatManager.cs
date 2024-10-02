class ChatManager
{
    private ChatManager()
    {

    }
    public static ChatManager Instance {
        get {
            if (_instance == null) {
                _instance = new ChatManager();
            }
            return _instance;
        }
    }

    private static ChatManager? _instance;
}