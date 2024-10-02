using System.Net;
using System.Net.Sockets;
using CloudStates;
using System.Text;

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

    // public ClientStates ClientState {
    //     get {
    //         return _clientState;
    //     }
    //     set {
    //         _clientState = value;
    //     }
    // }

    public void RunClient()
    {
        // Initialize State Machine
        WriteLine("Welcome to the Castle in the Clouds.\nConnecting you to the server...");
        _clientState = ClientStates.NO_CONNECTION;

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


    private static ClientInstance? _instance;
    private ClientInstance()
    {
        _serverIpEndPoint = new IPEndPoint(ServerAddress.SERVER_IP, ServerAddress.SERVER_PORT);
    }

    private IPEndPoint _serverIpEndPoint;

    private ClientStates _clientState;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;








    // ------------------- Big State Machine --------------- // 
    private void RunClientStateMachine()
    {
        bool isTerminated = false;

        while (!isTerminated) {
            switch (_clientState) {
                case ClientStates.NO_CONNECTION:
                    try {
                        Debug.WriteLine("State - NO_CONNECTION");
                        ConnectToServer();
                    }
                    catch (Exception e) {
                        Error.WriteLine("Failed to connect to the server: " + e.Message);
                        return;
                    }
                    break;

                case ClientStates.CHOOSING_AUTHENTICATE_METHOD:
                    Debug.WriteLine("State - CHOOSING_AUTHENTICATION_METHOD");
                    ChooseAuthenticateMethod();
                    break;

                case ClientStates.REGISTERING:
                    Debug.WriteLine("State - REGISTERING");
                    SendRegistrationInfo();
                    break;

                case ClientStates.REGISTRATION_INFO_SENT:
                    Debug.WriteLine("State - REGISTRATION_INFO_SENT");
                    HandleRegisterResponse();
                    break;

                case ClientStates.LOGGING_IN:
                    Debug.WriteLine("State - LOGGING_IN");
                    SendLoginInfo();
                    break;

                case ClientStates.LOGIN_INFO_SENT:
                    Debug.WriteLine("State - LOGIN_INFO_SENT");
                    HandleLoginResponse();
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
        _clientState = ClientStates.CHOOSING_AUTHENTICATE_METHOD;
    }

    private void SendFlag(ClientFlags clientFlag)
    {
        byte[] buffer = new byte[1];
        buffer[0] = (byte)clientFlag;
        _stream?.Write(buffer);
    }

    private void SendMessage(ClientFlags clientFlag, string message)
    {
        // TODO : implement this
        // Don't forget to convert the Message to UTF8 bytes via Encoding.UTF8.GetBytes();
        var messageBytes = Encoding.UTF8.GetBytes(message);
        byte[] buffer = new Byte[sizeof(ClientFlags) + messageBytes.Length];
        byte flagByte = (byte)clientFlag;
        buffer[0] = flagByte;
        Array.Copy(messageBytes, 0, buffer, 1, messageBytes.Length);
        _stream?.Write(buffer, 0, buffer.Length);
        return;
    }

    private void ChooseAuthenticateMethod()
    {
        int attempts = 0;
        while (true) {
            Write("[Login: l | Register: r | Quit: q] ");
            string? choice = ReadLine() ?? throw new Exception("Failed to read [userchoice] in ChooseAuthenticateMethod()");
            WriteLine();

            if (choice == "r") {
                SendFlag(ClientFlags.REGISTER_REQUEST);
                _clientState = ClientStates.REGISTERING;
                return;
            }
            else if (choice == "l") {
                SendFlag(ClientFlags.LOGIN_REQUEST);
                _clientState = ClientStates.LOGGING_IN;
                return;
            }
            else if (choice == "q"){
                SendFlag(ClientFlags.CLIENT_QUIT);
                _clientState = ClientStates.PROGRAM_CLOSED;
                return;
            }
            else {
                WriteLine("Invalid choice.");
                attempts+= 1;
            }
            if (attempts > AuthenticationRestrictions.MAX_AUTHENTICATION_CHOICE_MISTAKES) {
                WriteLine("Learn to read.");
                SendFlag(ClientFlags.CLIENT_QUIT);
            }
        }
    }

    private void SendRegistrationInfo()
    {
        WriteLine("Please provide a username and password, separated by a space. NOTE: Password is not encrypted.");
        WriteLine("Format: [username password]");
        string? credentials = ReadLine() ?? throw new Exception("Failed to read [username_password] in SendRegistrationInfo()");
        SendMessage(ClientFlags.SENDING_REGISTRATION_INFO, credentials);
        _clientState = ClientStates.REGISTRATION_INFO_SENT;
        return;
    }

    private void SendLoginInfo()
    {
        WriteLine("Please provide a username, followed by a password. Note: Password is not encrypted.");
        string? credentials = ReadLine() ?? throw new Exception("Failed to read [credentials] in SendLoginInfo");
        SendMessage(ClientFlags.SENDING_LOGIN_INFO, credentials);
        _clientState = ClientStates.LOGIN_INFO_SENT;
        return;
    }

    private void HandleRegisterResponse()
    {
        byte[] buffer = new byte[1];
        if (_stream == null) {
            throw new Exception("_stream was null in HandleRegisterResponse()");
        }
        int bytesRead = _stream.Read(buffer);
        Debug.Assert(bytesRead > 0);
        ServerFlags serverFlag = (ServerFlags)buffer[0];

        // Handling all possible responses...
        switch (serverFlag) {
            case ServerFlags.OK:
                WriteLine("Account created! Please proceed to login.");
                _clientState = ClientStates.CHOOSING_AUTHENTICATE_METHOD;
                return;
            case ServerFlags.USERNAME_TAKEN:
                WriteLine("Username was taken. Please choose another");
                _clientState = ClientStates.REGISTERING;
                return;
            case ServerFlags.TOO_MANY_ATTEMPTS:
                WriteLine("Too many attempts were made. You're probably trying to hack the database. Bye bye.");
                _clientState = ClientStates.PROGRAM_CLOSED;
                return;
            case ServerFlags.PASSWORD_TOO_LONG:
                WriteLine("Password was too long. Max length: " + AuthenticationRestrictions.MAX_PASSWORD_LENGTH + " characters.");
                _clientState = ClientStates.REGISTERING;
                return;
            case ServerFlags.USERNAME_TOO_LONG:
                WriteLine("Username was too long. Max length: " + AuthenticationRestrictions.MAX_USERNAME_LENGTH + " characters.");
                _clientState = ClientStates.REGISTERING;
                return;
            case ServerFlags.INCORRECT_CREDENTIALS_STRUCTURE:
                WriteLine("Credentials were passed in incorrectly. Try again.");
                _clientState = ClientStates.REGISTERING;
                return;
            default:
                throw new Exception($"invalid server response received in HandleRegisterResponse():\n{serverFlag}\n");
        }
    }

    private void HandleLoginResponse()
    {
        byte[] buffer = new byte[1];
        if (_stream == null) {
            throw new Exception("_stream was null in HandleRegisterResponse()");
        }
        _stream.Read(buffer);
        ServerFlags serverFlag = (ServerFlags)buffer[0];

        if (serverFlag == ServerFlags.OK) {
            WriteLine("Logged in successfully.");
            _clientState = ClientStates.LOGGED_IN;
            return;
        }
        else if (serverFlag == ServerFlags.PASSWORD_INCORRECT) {
            // TODO: Implement server-side attempt limiting.
            WriteLine("Incorrect password. You have 3 more tries.");
            _clientState = ClientStates.LOGGING_IN;
        }
        else {
            throw new Exception($"Invalid server response received in HandleLoginResponse():\n{serverFlag}\n");
        }
    }

}




































