#define CLIENT_LOGGING
#define DEBUG_ASSERTS


using System.Net.Sockets;
using System.Text;

class Client
{
    public static ClientNetworkInfo? connectionData;
    // --------------- Public Methods ------------ // 
    public static void Main()
    {
        // ---------- DEBUG LOGGING ----------- // 
#if CLIENT_LOGGING
        try {
            string currentDirectory = Directory.GetCurrentDirectory();
            if (!currentDirectory.EndsWith("CloudCastle")) {
                Console.WriteLine("When logging, please cd into the root of the solution, and run the program via the runClient.sh script");
                throw new Exception();
            }
            Console.WriteLine("Client logging is enabled.");
            _logFile = new StreamWriter(Path.Combine(Directory.GetCurrentDirectory(), "log_client.txt"));
        }
        catch (Exception e) {
            Console.WriteLine("Error upon opening logFile.txt: " + e.Message);
            _logFile?.Close();
            return;
        }
#endif
        // ------------------------------------- //

        LL("++++++CLIENT LOG+++++++");
        Console.WriteLine("Welcome to the Castle of Clouds!");

        try {
            RunCloudInstance();
        }
        catch (Exception e) {
            Console.WriteLine("Unexpected error in Main(): " + e.Message);
        }


        // ---------- DEBUG LOGGING ------- //
#if CLIENT_LOGGING
        _logFile?.Close();
#endif
        // ------------------------------------- //

        Console.WriteLine("Closing Client!");
        return;
    }
    // ------------------------------------------- // 

    // ------------- Private Members ------------- // 

    // ------------------------------------------- // 

    // ------------- Private Methods ------------- // 

    private static void RunCloudInstance()
    {
        try {
            Console.WriteLine("Trying to connect to the server...");
            ClientNetworkInfo clientNetworkInfo = new();
            using TcpClient client = new();
            client.Connect(clientNetworkInfo.ServerIpEndpoint);
            using NetworkStream stream = client.GetStream();

            Console.Write("Login - l | Register - r: ");
            string? choice = Console.ReadLine();
            if (choice == null) {
                throw new Exception("!!!Hacker detected!!!");
            }

            if (choice == "l") {
                LogInToServer(stream);
            }
            else if (choice == "r") {
                RegisterToServer(stream);
            }

            Console.WriteLine();
            LogInToServer(stream);
        }
        catch (Exception e) {
            Console.WriteLine("Error: " + e.Message);
        }



        return;
    }
    
    // This shit should be a state machine dude... This is why state machines were invented... Why dude.. Why...
    // Also, all these server responses would work well with flag bytes. Piss-easy in C, so shouldn't be difficult here.
    private static async void LogInToServer(NetworkStream stream)
    {
        int attempts = 0;
        bool loggedIn = false;
        int bytesRead;
        string response;
        byte[] buffer = new byte[1024];
        string loginRequest = "SYS_login_request";
        stream.Write(Encoding.UTF8.GetBytes(loginRequest));

        while (!loggedIn && attempts < 3) {
            Array.Clear(buffer);

            // Wait for acknowledgement from server to start the login process, unecessary.
            while ((bytesRead = stream.Read(buffer, 0, 1024)) > 0) { }

            response = Encoding.UTF8.GetString(buffer);

            // Attempt to log in via username and password.
            if (response == "OK") {
                string? username, password;
                Console.Write("Username: ");
                username = Console.ReadLine();
                Console.WriteLine();
                Console.Write("Password: ");
                password = Console.ReadLine();
                Console.WriteLine();

                string combined = username + " " + password;
                stream.Write(Encoding.UTF8.GetBytes(combined));

                Array.Clear(buffer);

                // Wait for acknowledgement from the server that the password and username was correct 
                while ((bytesRead = stream.Read(buffer, 0, 1024)) > 0) { }
                response = Encoding.UTF8.GetString(buffer);

                if (response == "LOGIN-OK") {
                    loggedIn = true;
                    break;
                }
            }
            else if (response == "IP-BANNED") {
                Console.WriteLine("Your IP is banned, lil bro.");
                return;
            }
            else if (response == "ACCOUNT-DELETED"){
                Console.Write("Uh oh! Due to inactivity, your account was deleted :)\nWould you like to create a new account?\nYes - y | No - n: ");
                string? userChoice = Console.ReadLine();
                Console.WriteLine();

                if (userChoice == "y"){
                    RegisterToServer(stream);
                }
                else {
                    Console.WriteLine("Bye then... :( You're not cool anyway...");
                    return;
                }
            }
            else if (response == "WRONG-CREDENTIALS"){
                Console.WriteLine($"Wrong credentials. You have {3 - attempts} more tries.");
            }

            attempts += 1;
        }
        if (loggedIn){
            Console.WriteLine("Login successful. Welcome to the Cloud Castle!");
            LaunchCloudDashboard(stream);
        }
        else {
            Console.WriteLine("Failed to login. Bye bye!");
        }
    }

    private static void RegisterToServer(NetworkStream stream)
    {
        return;
    }

    private static void LaunchCloudDashboard(NetworkStream stream){
        Console.WriteLine("Dashboard: unimplemented");
        return;
    }
    // ------------------------------------------- //



    // ------------ DEBUG ASSERT --------------- // 
#if DEBUG_ASSERTS
    public static void AA(Boolean statement)
    {
        System.Diagnostics.Debug.Assert(statement);
    }
#endif



    // ------------ DEBUG LOGGING -------------- // 
#if CLIENT_LOGGING
    private static StreamWriter? _logFile;
    // LL : Write to LogFile 
    public static void LL(string message)
    {
        _logFile?.WriteLine(message);
    }
#endif
    // ----------------------------------------- //


}