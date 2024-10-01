// ---------------- DESCRIPTION -------------- // 
// The manager of all connections.
// Responsible for closing threads, clearing the resources,
// Sending "hey, this connection has been closed" signals to the 
// client, etc.

class ServerManagement
{
    static ServerManagement()
    {
        _numberOfActiveConnections = 0;
    }

    public static int NumberOfActiveConnections {
        get;
    }

    public static void IncrementActiveConnections()
    {
        // May need locking. Depends on thread implementation
        _numberOfActiveConnections += 1;
    }
    public static void DecrementActiveConnections()
    {
        // May need locking. Depends on thread implementation
        _numberOfActiveConnections -= 1;
        if (_numberOfActiveConnections < 0)
            throw new Exception("DecrementActiveConnections(): Number of active connections is negative.");
        return;
    }

    private static int _numberOfActiveConnections;
}