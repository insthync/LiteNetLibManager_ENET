using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using LiteNetLibManager;
using ENet;

public class ENetTransport : ITransport
{
    private Host _client;
    private Peer _clientPeer;
    private Host _server;
    private readonly int _maxChannel;
    private readonly Dictionary<long, Peer> _serverPeers;
    public bool IsClientStarted
    {
        get { return _clientPeer.IsSet && _clientPeer.State == PeerState.Connected; }
    }
    public bool IsServerStarted
    {
        get { return _server != null && _server.IsSet; }
    }
    public int ServerPeersCount
    {
        get
        {
            if (_server.IsSet)
                return (int)_server.PeersCount;
            return 0;
        }
    }
    public int ServerMaxConnections { get; private set; }

    public bool HasImplementedPing
    {
        get { return true; }
    }

    public ENetTransport(int maxChannel)
    {
        _maxChannel = maxChannel;
        _serverPeers = new Dictionary<long, Peer>();
    }

    public bool StartClient(string address, int port)
    {
        if (IsClientStarted)
            return false;
        _client = new Host();
        Address addressData = new Address();
        addressData.SetHost(address);
        addressData.Port = (ushort)port;
        _client.Create();
        _clientPeer = _client.Connect(addressData, 4);
        return _clientPeer.IsSet;
    }

    public void StopClient()
    {
        if (_clientPeer.IsSet)
            _clientPeer.Disconnect(0);
        if (_client != null)
            _client.Dispose();
        _client = null;
    }

    public bool ClientReceive(out TransportEventData eventData)
    {
        eventData = default(TransportEventData);
        if (_client == null)
            return false;
        Event tempNetEvent;
        byte[] tempBuffers;
        _client.Service(0, out tempNetEvent);
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

    public bool ClientSend(byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
    {
        if (IsClientStarted)
        {
            Packet packet = default(Packet);
            packet.Create(writer.Data, writer.Length, GetPacketFlags(deliveryMethod));
            _clientPeer.Send(dataChannel, ref packet);
            return true;
        }
        return false;
    }

    public bool StartServer(int port, int maxConnections)
    {
        if (IsServerStarted)
            return false;
        ServerMaxConnections = maxConnections;
        _serverPeers.Clear();
        _server = new Host();
        Address address = new Address();
        address.Port = (ushort)port;
        _server.Create(address, maxConnections, _maxChannel);
        return true;
    }

    public bool ServerReceive(out TransportEventData eventData)
    {
        eventData = default(TransportEventData);
        if (_server == null)
            return false;
        Event tempNetEvent;
        byte[] tempBuffers;
        _server.Service(0, out tempNetEvent);
        switch (tempNetEvent.Type)
        {
            case EventType.None:
                return false;

            case EventType.Connect:
                eventData.type = ENetworkEvent.ConnectEvent;
                eventData.connectionId = tempNetEvent.Peer.ID;
                _serverPeers[tempNetEvent.Peer.ID] = tempNetEvent.Peer;
                break;

            case EventType.Disconnect:
                eventData.type = ENetworkEvent.DisconnectEvent;
                eventData.connectionId = tempNetEvent.Peer.ID;
                _serverPeers.Remove(tempNetEvent.Peer.ID);
                break;

            case EventType.Timeout:
                eventData.type = ENetworkEvent.DisconnectEvent;
                eventData.connectionId = tempNetEvent.Peer.ID;
                eventData.disconnectInfo = new DisconnectInfo()
                {
                    Reason = DisconnectReason.Timeout
                };
                _serverPeers.Remove(tempNetEvent.Peer.ID);
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

    public bool ServerSend(long connectionId, byte dataChannel, DeliveryMethod deliveryMethod, NetDataWriter writer)
    {
        if (IsServerStarted && _serverPeers.ContainsKey(connectionId) && _serverPeers[connectionId].State == PeerState.Connected)
        {
            Packet packet = default(Packet);
            packet.Create(writer.Data, writer.Length, GetPacketFlags(deliveryMethod));
            _serverPeers[connectionId].Send(dataChannel, ref packet);
            return true;
        }
        return false;
    }

    public bool ServerDisconnect(long connectionId)
    {
        if (IsServerStarted && _serverPeers.ContainsKey(connectionId))
        {
            _serverPeers[connectionId].Disconnect(0);
            _serverPeers.Remove(connectionId);
            return true;
        }
        return false;
    }

    public void StopServer()
    {
        if (_server != null)
            _server.Dispose();
        _server = null;
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

    public long GetClientRtt()
    {
        return _clientPeer.RoundTripTime;
    }

    public long GetServerRtt(long connectionId)
    {
        return _serverPeers[connectionId].RoundTripTime;
    }
}
