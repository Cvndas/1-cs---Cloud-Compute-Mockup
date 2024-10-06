using System.Net;
using System.Net.Sockets;
using CloudStates;
using System.Text;

// Used to store information about download/upload file requests across states.
struct FileInformation
{
    int fileSize;
    string fileName;
}
class ClientInstance
{
    // ------------------- PUBLIC ------------------------ //
    public static ClientInstance Instance {
        get {
            if (_instance == null) {
                _instance = new ClientInstance();
            }
            else {
                throw new Exception("Attempted to make another ClientInstance. There should only be one.");
            }
            return _instance;
        }
    }

    public void RunClient()
    {
        // Initialize State Machine
        _clientState = ClientStates.NO_CONNECTION;
        WriteLine("Welcome to the Castle in the Clouds.\nConnecting you to the server...");
        try {
            RunClientStateMachine();
        }
        catch (Exception e) {
            Error.WriteLine("Unexpected Exception: " + e.Message);
        }
        finally {
            WriteLine("Exiting the Castle in the Clouds.");
            if (_tcpClient != null) {
                _tcpClient.Close();
            }
            else {
                Error.WriteLine("_tcpClient was null.");
            }
            if (_stream != null) {
                _stream.Close();
            }
            else {
                Error.WriteLine("_stream was null.");
            }

        }
    }
    // ------------------- PRIVATE ------------------------ //

    // The server handles this as well, to protect itself against people who have just modified the client.
    // However, at the time of designing this, a purely server-side solution didn't work
    // for giving the client a smooth experience, so the same system is replicated client side.
    // Room for improvement, though low priority.
    private int _registrationAttempts;
    private int _loginAttempts;
    private CloudSenderReceiver _senderReceiver;


    private static ClientInstance? _instance;
    private ClientInstance()
    {
        _serverIpEndPoint = new IPEndPoint(ServerAddress.SERVER_IP, ServerAddress.SERVER_PORT);
        _registrationAttempts = 0;
        _loginAttempts = 0;
        _senderReceiver = new CloudSenderReceiver();
    }

    // ----------- CONNECTION INFO --------- //
    private IPEndPoint _serverIpEndPoint;
    private ClientStates _clientState;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    // ------------------------------------- //


    // ---------- SESSION INFO ------------- //
    private string? _username;
    private FileInformation? _currentFileToUpload;
    private FileInformation? _currentFileToDownload;
    private readonly string? _localStorageFolderPath;
    // ------------------------------------- //



    // ------------------- Big State Machine --------------- //
    private void RunClientStateMachine()
    {
        bool isTerminated = false;

        while (!isTerminated) {
            switch (_clientState) {
                case ClientStates.NO_CONNECTION:
                    try {
                        ConnectToServer();
                    }
                    catch (Exception e) {
                        Error.WriteLine("Failed to connect to the server: " + e.Message);
                        return;
                    }
                    break;

                case ClientStates.CHOOSING_AUTHENTICATE_METHOD:
                    ChooseAuthenticateMethod();
                    break;

                case ClientStates.REGISTERING:
                    SendRegistrationInfo();
                    break;

                case ClientStates.REGISTRATION_INFO_SENT:
                    HandleRegisterResponse();
                    if (_registrationAttempts > SystemConstants.MAX_REGISTRATION_ATTEMPTS) {
                        WriteLine("Too many registration attempts made.");
                        _clientState = ClientStates.PROGRAM_CLOSED;
                    }
                    break;

                case ClientStates.LOGGING_IN:
                    SendLoginInfo();
                    break;

                case ClientStates.LOGIN_INFO_SENT:
                    HandleLoginResponse();
                    if (_loginAttempts > SystemConstants.MAX_LOGIN_ATTEMPTS) {
                        WriteLine("Too many login attempts made.");
                        _clientState = ClientStates.PROGRAM_CLOSED;
                    }
                    break;

                case ClientStates.LOGGED_IN:
                    ChooseDashboardOption();
                    break;
                case ClientStates.IN_CHAT:
                    RunChatInterface();
                    break;
                case ClientStates.PROGRAM_CLOSED:
                    return;
                default:
                    throw new Exception("Invalid State Transition");
            }
        }

    }
    private void ConnectToServer()
    {
        _tcpClient = new TcpClient();
        _tcpClient.Connect(_serverIpEndPoint);
        _stream = _tcpClient.GetStream();
        _senderReceiver!.UpdateStream(_tcpClient.GetStream());
        ;

        bool isAssigned = false;
        int maxMessageSize = sizeof(CloudFlags) + SystemConstants.MAX_USERS_IN_QUEUE.ToString().Length + Encoding.UTF8.GetBytes("\n")[0];

        // Either you get OK, or you receive QUEUE_POSITION.
        while (!isAssigned) {
            List<(CloudFlags flag, string body)> serverResponses = _senderReceiver.ReceiveMessages();

            foreach ((CloudFlags flag, string body) in serverResponses) {
                if (flag == CloudFlags.SERVER_OK) {
                    Console.WriteLine("Connected to the server.");
                    isAssigned = true;
                    break;
                }
                else if (flag == CloudFlags.SERVER_QUEUE_POSITION) {
                    int positionInQueue;
                    if (!int.TryParse(body, out positionInQueue)) {
                        throw new Exception("Queue number was invalid.");
                    }
                    if (positionInQueue > SystemConstants.MAX_USERS_IN_QUEUE) {
                        WriteLine("Server is overloaded. Try again later.");
                    }
                    else {
                        WriteLine("Position in queue: " + positionInQueue);
                    }
                }
                else {
                    throw new Exception("Invalid flag received from the server.");
                }
            }

        }

        _clientState = ClientStates.CHOOSING_AUTHENTICATE_METHOD;
    }




    private void ChooseAuthenticateMethod()
    {
        int attempts = 0;
        while (true) {
            Write("[Login: l | Register: r | Quit: q] ");
            WriteLine();
            string? choice = ReadLine() ?? throw new Exception("Failed to read [userchoice] in ChooseAuthenticateMethod()");
            WriteLine();

            if (choice == "r") {
                _senderReceiver.SendMessage(CloudFlags.CLIENT_REGISTER_REQUEST, "");
                _clientState = ClientStates.REGISTERING;
                return;
            }
            else if (choice == "l") {
                _senderReceiver.SendMessage(CloudFlags.CLIENT_LOGIN_REQUEST, "");
                _clientState = ClientStates.LOGGING_IN;
                return;
            }
            else if (choice == "q") {
                _senderReceiver.SendMessage(CloudFlags.CLIENT_QUIT, "");
                _clientState = ClientStates.PROGRAM_CLOSED;
                return;
            }
            else {
                WriteLine("Invalid choice.");
                attempts += 1;
            }
            if (attempts > SystemConstants.MAX_AUTHENTICATION_CHOICE_MISTAKES) {
                WriteLine("Learn to read.");
                _senderReceiver.SendMessage(CloudFlags.CLIENT_QUIT, "");
            }
        }
    }

    private void SendRegistrationInfo()
    {
        WriteLine("Please provide a username and password, separated by a space. NOTE: Passwords are NOT encrypted.");
        WriteLine("Format: [username password]");
        string credentials = ReadLine() ?? throw new Exception("Failed to read [username_password] in SendRegistrationInfo()");
        _senderReceiver.SendMessage(CloudFlags.CLIENT_SENDING_REGISTRATION_INFO, credentials);
        _clientState = ClientStates.REGISTRATION_INFO_SENT;
    }

    private void SendLoginInfo()
    {
        WriteLine("Please provide a username and password, separated by a space. Note: Passwords are NOT encrypted.");
        WriteLine("Format: [username password]");
        string credentials = ReadLine() ?? throw new Exception("Failed to read [credentials] in SendLoginInfo");
        _senderReceiver.SendMessage(CloudFlags.CLIENT_SENDING_LOGIN_INFO, credentials);
        _clientState = ClientStates.LOGIN_INFO_SENT;

        // Mark the username on each attempt. Last attempt is a valid attempt, which will set a valid username.
        _username = credentials.Split(" ")[0] ?? "";
    }

    private void HandleRegisterResponse()
    {
        List<(CloudFlags flag, string body)> receivedList = _senderReceiver.ReceiveMessages();
        if (receivedList.Count > 1 || receivedList[0].body != "") {
            throw new Exception("Received invalid response in HandleRegisterResponse()");
        }
        CloudFlags serverFlag = receivedList[0].flag;
        if (serverFlag == CloudFlags.SERVER_OK) {
            WriteLine("Account created! Please proceed to login.");
            _clientState = ClientStates.CHOOSING_AUTHENTICATE_METHOD;
            return;
        }
        _registrationAttempts += 1;
        // Handling all other responses
        switch (serverFlag) {
            case CloudFlags.SERVER_USERNAME_TAKEN:
                WriteLine("Username was taken. Please choose another");
                _clientState = ClientStates.REGISTERING;
                return;
            case CloudFlags.SERVER_TOO_MANY_ATTEMPTS:
                WriteLine("Too many attempts were made. You're probably trying to hack the database. Bye bye.");
                _clientState = ClientStates.PROGRAM_CLOSED;
                return;
            case CloudFlags.SERVER_PASSWORD_TOO_LONG:
                WriteLine("Password was too long. Max length: " + SystemConstants.MAX_PASSWORD_LENGTH + " characters.");
                _clientState = ClientStates.REGISTERING;
                return;
            case CloudFlags.SERVER_USERNAME_TOO_LONG:
                WriteLine("Username was too long. Max length: " + SystemConstants.MAX_USERNAME_LENGTH + " characters.");
                _clientState = ClientStates.REGISTERING;
                return;
            case CloudFlags.SERVER_INCORRECT_CREDENTIALS_STRUCTURE:
                WriteLine("Credentials were passed in incorrectly.");
                _clientState = ClientStates.REGISTERING;
                return;
            case CloudFlags.SERVER_UNEXPECTED_SERVER_ERROR:
                WriteLine("The server has experienced an unexpected error. Try again, or quit.");
                _clientState = ClientStates.CHOOSING_AUTHENTICATE_METHOD;
                return;
            default:
                throw new Exception($"invalid server response received in HandleRegisterResponse():\n{serverFlag}\n");
        }
    }

    private void HandleLoginResponse()
    {
        List<(CloudFlags flag, string body)> receivedList = _senderReceiver.ReceiveMessages();
        if (receivedList.Count > 1 || receivedList[0].body != "") {
            throw new Exception("Received invalid response in HandleRegisterResponse()");
        }
        CloudFlags serverFlag = receivedList[0].flag;

        if (serverFlag == CloudFlags.SERVER_OK) {
            SetupUserSession();
            _clientState = ClientStates.LOGGED_IN;
            return;
        }

        _loginAttempts += 1;
        // Unsuccessful cases
        switch (serverFlag) {
            case CloudFlags.SERVER_ALREADY_LOGGED_IN:
                WriteLine("You are already logged in on another client.");
                return;
            case CloudFlags.SERVER_PASSWORD_INCORRECT:
                WriteLine($"Incorrect password. You have {SystemConstants.MAX_LOGIN_ATTEMPTS - _loginAttempts + 1} more chances to log in.");
                _clientState = ClientStates.CHOOSING_AUTHENTICATE_METHOD;
                return;
            case CloudFlags.SERVER_INCORRECT_CREDENTIALS_STRUCTURE:
                WriteLine("");
                _clientState = ClientStates.CHOOSING_AUTHENTICATE_METHOD;
                return;
            case CloudFlags.SERVER_USERNAME_DOESNT_EXIST:
                WriteLine("Username does not exist");
                _clientState = ClientStates.CHOOSING_AUTHENTICATE_METHOD;
                return;
            case CloudFlags.SERVER_TOO_MANY_ATTEMPTS:
                // This line can only be reached if I made a coding mistake, or somebody manipulated the
                // client code.
                WriteLine("Did you modify the client, lil bro?");
                _clientState = ClientStates.PROGRAM_CLOSED;
                return;
            case CloudFlags.SERVER_UNEXPECTED_SERVER_ERROR:
                WriteLine("The server has experienced an unexpected error. Try again, or quit.");
                _clientState = ClientStates.CHOOSING_AUTHENTICATE_METHOD;
                return;
            default:
                throw new Exception($"Invalid server response received in HandleLoginResponse():\n{serverFlag}\n");
        }
    }

    private void SetupUserSession()
    {
        // TODO - Implement
        // Need to make the upload and download info structs not null, and need to set the right filepath for the local folder,
        // and make the folder if it doesn't exist yet.

        // Note: Each client can store multiple local users.
        // TODO End of project: Delete local folders too if the account was purged from the cloud.
        WriteLine("UNIMPLEMENTED: SetupSession - Needed for file upload and download and local file view");

        // This assert checks if the username assignment in SendLoginInfo() was correct.
        Debug.Assert(_username != null && _username != "");
        return;
    }

    private void ChooseDashboardOption()
    {
        // TODO HIGH PRIORITY
        while (true) {
            WriteLine("[-] +++ +++ Welcome to the Dashboard +++ +++ [-]");
            WriteLine("View local files: l | View files in cloud: c | Upload file: [u filename] | Download file: [d filename] | Enter Chat: chat");
            string? userChoice = Console.ReadLine();

            if (userChoice == null) {
                Error.WriteLine("Unexpected error when receiving userchoice.");
                continue;
            }

            if (userChoice == "l") {
                ShowLocalFiles();
            }
            else if (userChoice == "c") {
                ViewCloudFiles();
            }
            else if (userChoice.StartsWith("u")) {
                // Let the server handle the issue of the request being incorrectly formatted, as it has to do it anyway.
                RequestUpload(request: userChoice);
            }
            else if (userChoice.StartsWith("d")) {
                // Let the server handle the issue of the request being incorrectly formatted, as it has to do it anyway.
                // This method changes the state, so breaking out of ChooseDashboardOption()
                RequestDownload(request: userChoice);
                break;
            }
            else if (userChoice == "chat") {
                _senderReceiver.SendMessage(CloudFlags.CLIENT_TO_CHAT, "");
                _clientState = ClientStates.IN_CHAT;
                break;
            }
        }
    }

    private void ShowLocalFiles()
    {
        // TODO - Implement
        return;
    }
    private void ViewCloudFiles()
    {
        // TODO - Implement, based on ShowLocalFiles, but with extra bells and whistles.
        return;
    }

    private void RequestUpload(string request)
    {
        // TODO - Implement, after local file view
    }

    // Requests a download.
    private void RequestDownload(string request)
    {
        // TODO - Implement, after file upload.
        // Send the request to the server, and change the state of the client based on the response.


        // Send request to file

        // If request is rejected, state = back to LOGGED_IN
        // If request is accepted, state = AWAITING_FILE_DOWNLOAD, don't want to be interrupted,
        // though to be fair there won't be any file corruption as we're working with small files,
        // everything can be stored into memory, so we can just load the entire received bytes into memory
        // before writing to disk. If we don't receive all bytes, we don't write to disk, so we don't write
        // anything that's corrupted.
    }

    /// <summary>
    /// Volatile to prevent stale-reads. 
    /// Only ever set by RunChatInterface(), only ever read by ReceiveMessagesJob
    /// </summary>
    private void RunChatInterface()
    {
        // Idea: Main thread receives user input and sends it to the server
        //       helper thread receives messages from the server and displays it to the user.
        if (_stream == null) {
            throw new Exception("RunChatInterface: _stream was null.");
        }

        Console.Write("Welcome to the Cloud Chat, " + _username + ".");
        Console.WriteLine(" (type \"quit\" to return to Dashboard.)");
        Thread receiveThread = new Thread(() => ReceiveMessagesJob());
        try {
            receiveThread.Start();
            string? userMessage = "";
            while (true) {
                Write("Send message: ");
                userMessage = ReadLine();
                if (userMessage == null) {
                    throw new Exception("User's input was null in RunChatInterface");
                }
                if (userMessage == "quit") {
                    _senderReceiver.SendMessage(CloudFlags.CLIENT_TO_DASHBOARD, "");
                    receiveThread.Interrupt();

                    // TODO : Make the user skip registration. For later, though. 
                    _clientState = ClientStates.LOGGED_IN;
                    break;
                }
                if (userMessage.Length > SystemConstants.MAX_CHAT_MESSAGE_LEN) {
                    WriteLine("Chat message too long. Max length: " + SystemConstants.MAX_CHAT_MESSAGE_LEN);
                }
                else {
                    string formattedChatMessage = FormatChatMessage(userMessage);
                    _senderReceiver.SendMessage(CloudFlags.CLIENT_CHAT_MESSAGE, formattedChatMessage);
                }
            }
        }
        catch (IOException e) {
            Console.WriteLine("Server has become unresponsive. Exiting the program.");
            receiveThread.Interrupt();
            receiveThread.Join();
            throw e;
        }
        receiveThread.Join();
        WriteLine("Returning to dashboard.");
    }

    private string FormatChatMessage(string userMessage)
    {
        string ret = _username + SystemConstants.CHAT_MESSAGE_PREAMBLE + userMessage;
        Debug.Assert(ret.Length <= SystemConstants.MAX_FORMATTED_CHAT_MESSAGE_BODY_LEN);
        return ret;
    }

    /// <summary>
    /// Thread: Chat Client Helper thread. <br/>Launched in RunChatInterface<br/>Joined in RunChatInterface.
    /// </summary>
    private void ReceiveMessagesJob()
    {
        try {

            while (true) {
                List<(CloudFlags flag, string body)> receivedChats = _senderReceiver.ReceiveMessages();
                // Parse the data into a list of Messages, separated by strings that match Flags.CHAT_MESSAGE
                foreach (var chat in receivedChats) {
                    if (chat.flag != CloudFlags.SERVER_CHAT_MESSAGE) {
                        throw new Exception("ReceiveMessagesJob: Received incorrect flag from server.");
                    }
                    WriteLine(chat.body);
                }
            }
        }
        catch (ThreadInterruptedException) {
            Debug.WriteLine("User is no longer in chat. Closing the ReceiveMessagesJob.");
            return;
        }
    }
}
