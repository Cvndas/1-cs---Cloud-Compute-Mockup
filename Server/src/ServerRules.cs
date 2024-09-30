class ServerRules
{
    public const int MAX_IMAGES_PER_USER = 5;
    public const int MAX_TEXT_FILES_PER_USER = 5;
    public const int CACHE_NUMBER_OF_TEXT_FILES= 3; 
    public const int CACHE_NUMBER_OF_IMAGES = 2;
    
    // A 5th person "John" who tries to connect, will wait until either
    // room is made, or John presses q.
    public const int MAX_NUMBER_OF_CONNECTIONS = 4;

    // When an 8th person signs up, the last person to have signed onto the server
    // has their account purged. When attempting to log in, that person receives
    // one message stating that their account has been deleted, and that they
    // need to make a new one. 
    public const int MAX_NUMBER_OF_ACCOUNTS = 7;
}