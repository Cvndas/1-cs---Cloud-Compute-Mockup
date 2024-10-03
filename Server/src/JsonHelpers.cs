using System.Text.Json;
// All methods in here assume your thread has control over the json object that these actions are performed on
class JsonHelpers
{

    // NOTE - You must own the lock over the filePath
    public static bool KeyExists(in string filePath, in string username)
    {
        // TODO JSON
        string existingData = File.ReadAllText(filePath);
        if (existingData == "") {
            return false;
        }
        else {
            Dictionary<string, string> existingDataDictionary =
                JsonSerializer.Deserialize<Dictionary<string, string>>(existingData)
                ?? 
                throw new Exception($"Thread {Thread.CurrentThread.ManagedThreadId}: JsonHelpers.KeyExists() dictionary was null");

            if (existingDataDictionary.ContainsKey(username))
                return true;
            return false;
        }
    }

    // NOTE - You must own the lock over the filePath
    public static void RemoveKeyPair(in string Key)
    {
        // TODO JSON
        return;
    }

    // NOTE - You must own the lock over the filePath
    public static void AddKeyPair(in string filePath, in string key, in string value)
    {
        string existingData = File.ReadAllText(filePath);
        Dictionary<string, string>? existingDataDictionary;
        if (existingData == "") {
            Debug.WriteLine("Ok: registeredUsers.json was empty.");
            existingDataDictionary = new Dictionary<string, string> {
                    { key, value }
                };
        }
        else {
            existingDataDictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(existingData);
            // if this fucks up, it'll throw an exception. Server handles it. KeyExists() does this the proper way. 
            existingDataDictionary!.Add(key, value);
        }
        existingData = JsonSerializer.Serialize(existingDataDictionary,
                            new JsonSerializerOptions() { WriteIndented = true });
        File.WriteAllText(filePath, existingData);
    }

    public static bool ValueMatchesKey(in string key, in string value, in string filePath)
    {
        string existingData = File.ReadAllText(filePath);
        if (existingData == "") {
            return false;
        }
        else {
            Dictionary<string, string> existingDataDictionary =
                JsonSerializer.Deserialize<Dictionary<string, string>>(filePath)
                ??
                throw new Exception($"Thread {Thread.CurrentThread.ManagedThreadId} created a null dictionary in ValueMatchesKey.");
            string? valueInFile;
            if (existingDataDictionary.TryGetValue(key, out valueInFile)){
                if (valueInFile == value){
                    return true;
                }
                return false;
            }
        }
        return false;
    }
}