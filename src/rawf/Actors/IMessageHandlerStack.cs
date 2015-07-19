namespace rawf.Actors
{
    public interface IMessageHandlerStack
    {
        void Push(MessageHandlerIdentifier messageHandlerIdentifier, SocketIdentifier socketIdentifier);
        SocketIdentifier Pop(MessageHandlerIdentifier messageHandlerIdentifier);
    }
}