internal class CloudEmployee
{
    public bool _hasWork;
    public CloudEmployee(){
        _employeeThread = new Thread(RunCloudEmployeeThread);
        _employeeThread.Start();
    }

    public void AssignClient(ClientResources clientResources){
        _clientResources = clientResources;
        Program.CloudAssert(_clientResources != null);
        return;
    }

    private Thread _employeeThread;

    private ClientResources? _clientResources;

    private void transferResourcesToChatEmployee(ChatEmployee chatEmployee){
        // TODO Implent for Chat
        return;
    }

    private void RunCloudEmployeeThread(){
        // TODO: Implement for Login

        // First wait on conditional variable _hasWork, and then
        RunCloudEmployeeStateMachine();

        return;
    }

    private void RunCloudEmployeeStateMachine(){

    }

}