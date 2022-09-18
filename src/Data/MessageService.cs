namespace DBAnonymizer;

public class MessageService
{
    public event Func<object, MessageEventArgs, Task>? MessageReceived;

    public void SendMessage(string message) => 
        MessageReceived?.Invoke(this, new MessageEventArgs { Message = message });
}