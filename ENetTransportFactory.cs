using LiteNetLibManager;

public class ENetTransportFactory : BaseTransportFactory
{
    public override bool CanUseWithWebGL { get { return false; } }
    public int maxChannel = 4;

    public override ITransport Build()
    {
        return new ENetTransport(maxChannel);
    }
}
