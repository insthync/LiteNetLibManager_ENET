using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using LiteNetLibManager;
using ENet;

public class ENetTransport : ITransport
{
    private Host client;
    private Peer clientPeer;
    private Host server;
    private readonly int maxChannel;
    private readonly Dictionary<long, Peer> serverPeers;
    public bool IsClientStarted
    {
        get { return clientPeer.IsSet && clientPeer.State == PeerState.Connected; }
    }
    public bool IsServerStarted
    {
        get { return server != null && server.IsSet; }
    }
    public int ServerPeersCount
    {
        get
        {
            if (server.IsSet)
                return (int)server.PeersCount;
            return 0;
        }
    }
    public int ServerMaxConnections { get; private set; }

    public ENetTransport(int maxChannel)
    {
        this.maxChannel = maxChannel;
        serverPeers = new Dictionary<long, Peer>();
    }

    public bool StartClient(string address, int port)
    {
        if (IsClientStarted)
            return false;
        client = new Host();
        Address addressData = new Address();
        addressData.SetHost(address);
        addressData.Port = (ushort)port;
        client.Create();
        clientPeer = client.Connect(addressData, 4);
        return clientPeer.IsSet;
    }

    public void StopClient()
    {
        if (clientPeer.IsSet)
            clientPeer.Disconnect(0);
        if (client != null)
            client.Dispose();
        client = null;
    }

    public bool ClientReceive(out TransportEventData eventData)
    {
        eventData = default(TransportEventData);
        if (client == null)
            return false;
        Event tempNetEvent;
        byte[] tempBuffers;
        client.Service(0, out tempNetEvent);
        switch (tempNetEvent.Type)
        {
            case EventType.None:
                return false;

            case EventType.Connect:
                eventData.type = ENetworkEvent.ConnectEvent;
                eventData.connectionId = tempNetEvent.Peer.ID;
                break;

            case EventType.Disconnect:
                eventData.type = ENetworkEvent.DisconnectEvent;
                eventData.connectionId = tempNetEvent.Peer.ID;
                break;

            case EventType.Timeout:
                eventData.type = ENetworkEvent.DisconnectEvent;
                eventData.connectionId = tempNetEvent.Peer.ID;
                eventData.disconnectInfo = new DisconnectInfo()
                {
                    Reason = DisconnectReason.Timeout
                };
                break;

            case EventType.Receive:
                tempBuffers = new byte[tempNetEvent.Packet.Length];
                tempNetEvent.Packet.CopyTo(tempBuffers);

                eventData.type = ENetworkEvent.DataEvent;
                eventData.connectionId = tempNetEvent.Peer.ID;
                eventData.reader = new NetDataReader(tempBuffers);
                tempNetEvent.Packet.Dispose();
                break;
        }
        return true;
    }

    public bool ClientSend(byte dataChannel, DeliveryMethod deliveryMethod, byte[] data)
    {
        if (IsClientStarted)
        {
            Packet packet = default(Packet);
            packet.Create(data, data.Length, GetPacketFlags(deliveryMethod));
            clientPeer.Send(dataChannel, ref packet);
            return true;
        }
        return false;
    }

    public bool StartServer(int port, int maxConnections)
    {
        if (IsServerStarted)
            return false;
        ServerMaxConnections = maxConnections;
        serverPeers.Clear();
        server = new Host();
        Address address = new Address();
        address.Port = (ushort)port;
        server.Create(address, maxConnections, maxChannel);
        return true;
    }

    public bool ServerReceive(out TransportEventData eventData)
    {
        eventData = default(TransportEventData);
        if (server == null)
            return false;
        Event tempNetEvent;
        byte[] tempBuffers;
        server.Service(0, out tempNetEvent);
        switch (tempNetEvent.Type)
        {
            case EventType.None:
                return false;

            case EventType.Connect:
                eventData.type = ENetworkEvent.ConnectEvent;
                eventData.connectionId = tempNetEvent.Peer.ID;
                serverPeers[tempNetEvent.Peer.ID] = tempNetEvent.Peer;
                break;

            case EventType.Disconnect:
                eventData.type = ENetworkEvent.DisconnectEvent;
                eventData.connectionId = tempNetEvent.Peer.ID;
                serverPeers.Remove(tempNetEvent.Peer.ID);
                break;

            case EventType.Timeout:
                eventData.type = ENetworkEvent.DisconnectEvent;
                eventData.connectionId = tempNetEvent.Peer.ID;
                eventData.disconnectInfo = new DisconnectInfo()
                {
                    Reason = DisconnectReason.Timeout
                };
                serverPeers.Remove(tempNetEvent.Peer.ID);
                break;

            case EventType.Receive:
                tempBuffers = new byte[tempNetEvent.Packet.Length];
                tempNetEvent.Packet.CopyTo(tempBuffers);

                eventData.type = ENetworkEvent.DataEvent;
                eventData.connectionId = tempNetEvent.Peer.ID;
                eventData.reader = new NetDataReader(tempBuffers);
                tempNetEvent.Packet.Dispose();
                break;
        }
        return true;
    }

    public bool ServerSend(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, byte[] data)
    {
        if (IsServerStarted && serverPeers.ContainsKey(connectionId) && serverPeers[connectionId].State == PeerState.Connected)
        {
            Packet packet = default(Packet);
            packet.Create(data, data.Length, GetPacketFlags(deliveryMethod));
            serverPeers[connectionId].Send(dataChannel, ref packet);
            return true;
        }
        return false;
    }

    public bool ServerDisconnect(long connectionId)
    {
        if (IsServerStarted && serverPeers.ContainsKey(connectionId))
        {
            serverPeers[connectionId].Disconnect(0);
            serverPeers.Remove(connectionId);
            return true;
        }
        return false;
    }

    public void StopServer()
    {
        if (server != null)
            server.Dispose();
        server = null;
    }

    public void Destroy()
    {
        StopClient();
        StopServer();
    }

    public PacketFlags GetPacketFlags(DeliveryMethod deliveryMethod)
    {
        switch (deliveryMethod)
        {
            case DeliveryMethod.ReliableOrdered:
            case DeliveryMethod.ReliableUnordered:
            case DeliveryMethod.ReliableSequenced:
                return PacketFlags.Reliable;
            case DeliveryMethod.Sequenced:
                return PacketFlags.None;
            default:
                return PacketFlags.Unsequenced;
        }
    }
}
