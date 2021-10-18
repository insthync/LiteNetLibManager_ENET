using LiteNetLibManager;

public class ENetTransportFactory : BaseTransportFactory
{
    public int maxChannel = 4;

    public override ITransport Build()
    {
        return new ENetTransport(maxChannel);
    }
}
