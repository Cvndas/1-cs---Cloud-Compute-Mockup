class ServerRules
{
    // While the 10 most active users may store their data on the server, only the 5 most active 
    // users may use the cloud storage service at any point in time.
    // 5 users may be active in chat at once

    // By performing a single action on your personal database
    public const int CLOUD_EMPLOYEE_COUNT = 5;
    public const int CHAT_EMPLOYEE_COUNT = 5;
    public const int MAX_USER_RECORDS = 10;
}