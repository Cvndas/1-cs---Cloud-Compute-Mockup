using System.Text;
using System.Net;
using System.Net.Sockets;


namespace CloudStates;
public class CloudSenderReceiver
{
    private static readonly char MESSAGE_TERMINATOR = '\n';
    private static readonly int MESSAGE_TERMINATOR_BYTE_LEN = 1;
    private static readonly int FLAG_BYTE_LENGTH = sizeof(CloudFlags);
    private static readonly int MAX_MESSAGE_BYTE_LEN = FLAG_BYTE_LENGTH + SystemConstants.MAX_BODY_BYTE_LEN + MESSAGE_TERMINATOR_BYTE_LEN;

    private static readonly int RECEIVE_BUFFER_SIZE = 16384;

    private byte[] _receiveBuffer;
    private NetworkStream? _stream;

    public CloudSenderReceiver()
    {
        _receiveBuffer = new byte[RECEIVE_BUFFER_SIZE];
    }

    public void UpdateStream(NetworkStream stream)
    {
        _stream = stream;
    }

    public List<(CloudFlags flagtype, string body)> ReceiveMessages()
    {
        try {
            Array.Clear(_receiveBuffer); // Make sure you don't receive data from previous reads that may have been interrupted.
            var ret = new List<(CloudFlags, string)>(5);
            System.Diagnostics.Debug.Assert(_stream!.CanRead);

            // Read all data that is available
            int bytesReceived = 0;
            do {
                if (bytesReceived == RECEIVE_BUFFER_SIZE) {
                    Array.Clear(_receiveBuffer);
                    throw new ReadBufferTooSmallException("Failed to read all messages available in stream. Buffer was too small.");
                }
                bytesReceived += _stream!.Read(_receiveBuffer, 0, MAX_MESSAGE_BYTE_LEN);
            } while (_stream.DataAvailable);

            if (bytesReceived == 0) {
                throw new IOException("Stream data was not readable.");
            }


            // Split it up into distinct messages, and add each into the return list.
            string allDataStr = Encoding.UTF8.GetString(_receiveBuffer, 0, bytesReceived);
            string[] allDataStrSplit = allDataStr.Split(MESSAGE_TERMINATOR);
            System.Diagnostics.Debug.Assert(allDataStrSplit.Length > 0);

            foreach (string message in allDataStrSplit) {
                if (message == "") { // If the end of the list has been reached.
                    break;
                }
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                CloudFlags flag = (CloudFlags)messageBytes[0];

                int messageBodyLen = messageBytes.Length - FLAG_BYTE_LENGTH;

                if (messageBodyLen > SystemConstants.MAX_BODY_BYTE_LEN) {
                    System.Diagnostics.Debug.WriteLine("Received a message whose body exceeded the maximum allowed body length. It has been ignored.");
                    continue;
                }

                string bodyString = "";

                if (messageBodyLen > 0) {
                    byte[] bodyBytes = new byte[messageBodyLen];
                    Array.Copy(messageBytes, FLAG_BYTE_LENGTH, bodyBytes, 0, messageBodyLen);
                    bodyString = Encoding.UTF8.GetString(bodyBytes);
                }

                ret.Add((flag, bodyString));
            }

            return ret;
        }
        catch (ThreadInterruptedException e){
            // Consume all data before exiting. 
            while (_stream!.DataAvailable){
                _stream!.Read(_receiveBuffer, 0, MAX_MESSAGE_BYTE_LEN);
            }
            throw e;
        }
    }

    public void SendMessage(CloudFlags flagtype, string body)
    {
        body.ReplaceLineEndings("");
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        byte[] sendBuffer = new byte[FLAG_BYTE_LENGTH + bodyBytes.Length + MESSAGE_TERMINATOR_BYTE_LEN];
        sendBuffer[0] = (byte)flagtype;
        int bodyPosition = FLAG_BYTE_LENGTH;

        Array.Copy(bodyBytes, 0, sendBuffer, bodyPosition, bodyBytes.Length);

        int terminatorPosition = FLAG_BYTE_LENGTH + bodyBytes.Length;
        sendBuffer[terminatorPosition] = (byte)MESSAGE_TERMINATOR;

        _stream!.Write(sendBuffer);
    }
}

public class ReadBufferTooSmallException : Exception
{
    public ReadBufferTooSmallException(string message) : base(message)
    {

    }
}