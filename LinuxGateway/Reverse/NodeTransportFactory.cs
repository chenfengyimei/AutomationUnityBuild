namespace LinuxGateway.Reverse;

public sealed class NodeTransportFactory(
    DirectNodeTransport directTransport,
    ReverseNodeTransport reverseTransport)
{
    public INodeTransport Create(GatewayNodeRecord node)
    {
        return node.ConnectionMode == ReverseConnectionModes.Reverse
            ? reverseTransport
            : directTransport;
    }
}
