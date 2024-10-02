

internal class CloudManager
{
    public static CloudManager Instance {
        get {
            if (_instance == null) {
                _instance = new CloudManager();
            }
            return _instance;
        }
    }

    public void CreateCloudEmployeePool()
    {
        for (int i = 0; i < ServerRules.CLOUD_EMPLOYEE_COUNT; i++) {
            AddToFreeList(new CloudEmployee());
        }
    }


    public void AddToFreeList(CloudEmployee cloudEmployee)
    {
        // TODO : Implement a proper identification system, so a thread can
        // add itself in here.
        lock (_freeListLock) {
            _freeList.AddToQueue(cloudEmployee);
        }
    }

    // Returns null is the freelist was empty.
    public CloudEmployee? PopFromFreeList()
    {
        CloudEmployee? ret;
        lock (_freeListLock) {
            ret = _freeList.PopHead();
        }
        return ret;
    }

    // ----------------- PRIVATE ------------------ // 
    private static CloudManager? _instance;
    private CloudManager()
    {
        _freeList = FreeCloudEmployees.Instance;
        _freeListLock = new object();
    }

    private FreeCloudEmployees _freeList;
    private readonly object _freeListLock;





    // ------------------- FREELIST --------------- // 
    // Class that serves as the container for the FreeList. 
    // Idea: Inheritance to implement the same thing for the chat?
    private class FreeCloudEmployees
    {
        public static FreeCloudEmployees Instance {
            get {
                if (_instance == null) {
                    _instance = new FreeCloudEmployees();
                }
                return _instance;
            }
        }

        public void AddToQueue(CloudEmployee cloudEmployee)
        {
            if (cloudEmployee != null) {
                _freeList.Enqueue(cloudEmployee);
            }
            else {
                throw new Exception("Attempted to enqueue a null cloudEmployee to freeList");
            }
        }

        public CloudEmployee? PopHead()
        {
            bool freeThreadExisted = _freeList.TryDequeue(out CloudEmployee? ret);
            if (freeThreadExisted) {
                return ret;
            }
            else {
                return null;
            }
        }

        private static FreeCloudEmployees? _instance;

        private FreeCloudEmployees()
        {
            _freeList = new Queue<CloudEmployee>();
        }

        private Queue<CloudEmployee> _freeList;


    }
}

