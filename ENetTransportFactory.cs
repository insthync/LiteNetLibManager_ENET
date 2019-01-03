using LiteNetLibManager;

public class ENetTransportFactory : BaseTransportFactory
{
    public override ITransport Build()
    {
        return new ENetTransport();
    }
}
