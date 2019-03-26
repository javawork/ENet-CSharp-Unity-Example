using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ENet;
using Server;
using Random = UnityEngine.Random;

public class SceneManager : MonoBehaviour {

    public GameObject myPlayerFactory;
    public GameObject otherPlayerFactory;

    private GameObject _myPlayer;
    private uint _myPlayerId;

    private Host _client;
    private Peer _peer;
    private int _skipFrame = 0;
    private Dictionary<uint, GameObject> _players = new Dictionary<uint, GameObject>();

    const int channelID = 0;

    void Start ()
    {
        Application.runInBackground = true;
        InitENet();
        _myPlayer = Instantiate(myPlayerFactory);
    }

	void Update ()
    {
    }

    void FixedUpdate()
    {
        UpdateENet();

        if (++_skipFrame < 3)
            return;

        SendPositionUpdate();
        _skipFrame = 0;
    }

    void OnDestroy()
    {
        _client.Dispose();
        ENet.Library.Deinitialize();
    }

    private void InitENet()
    {
        const string ip = "127.0.0.1";
        const ushort port = 6005;
        ENet.Library.Initialize();
        _client = new Host();
        Address address = new Address();

        address.SetHost(ip);
        address.Port = port;
        _client.Create();
        Debug.Log("Connecting");
        _peer = _client.Connect(address);
    }

    private void UpdateENet()
    {
        ENet.Event netEvent;

        if (_client.CheckEvents(out netEvent) <= 0)
        {
            if (_client.Service(15, out netEvent) <= 0)
                return;
        }

        switch (netEvent.Type)
        {
            case ENet.EventType.None:
                break;

            case ENet.EventType.Connect:
                Debug.Log("Client connected to server - ID: " + _peer.ID);
                SendLogin();
                break;

            case ENet.EventType.Disconnect:
                Debug.Log("Client disconnected from server");
                break;

            case ENet.EventType.Timeout:
                Debug.Log("Client connection timeout");
                break;

            case ENet.EventType.Receive:
                Debug.Log("Packet received from server - Channel ID: " + netEvent.ChannelID + ", Data length: " + netEvent.Packet.Length);
                ParsePacket(ref netEvent);
                netEvent.Packet.Dispose();
                break;
        }
    }

    enum PacketId : byte
    {
        LoginRequest = 1,
        LoginResponse = 2,
        LoginEvent = 3,
        PositionUpdateRequest = 4,
        PositionUpdateEvent = 5,
        LogoutEvent = 6
    }

    private void SendPositionUpdate()
    {
        var x = _myPlayer.GetComponent<Rigidbody2D>().position.x;
        var y = _myPlayer.GetComponent<Rigidbody2D>().position.y;

        var protocol = new Protocol();
        var buffer = protocol.Serialize((byte)PacketId.PositionUpdateRequest, _myPlayerId, x, y);
        var packet = default(Packet);
        packet.Create(buffer);
        _peer.Send(channelID, ref packet);
    }

    private void SendLogin()
    {
        Debug.Log("SendLogin");
        var protocol = new Protocol();
        var buffer = protocol.Serialize((byte)PacketId.LoginRequest, 0);
        var packet = default(Packet);
        packet.Create(buffer);
        _peer.Send(channelID, ref packet);
    }

    private void ParsePacket(ref ENet.Event netEvent)
    {
        var readBuffer = new byte[1024];
        var readStream = new MemoryStream(readBuffer);
        var reader = new BinaryReader(readStream);

        readStream.Position = 0;
        netEvent.Packet.CopyTo(readBuffer);
        var packetId = (PacketId)reader.ReadByte();

        Debug.Log("ParsePacket received: " + packetId);

        if (packetId == PacketId.LoginResponse)
        {
            _myPlayerId = reader.ReadUInt32();
            Debug.Log("MyPlayerId: " + _myPlayerId);
        }
        else if (packetId == PacketId.LoginEvent)
        {
            var playerId = reader.ReadUInt32();
            Debug.Log("OtherPlayerId: " + playerId);
            SpawnOtherPlayer(playerId);
        }
        else if (packetId == PacketId.PositionUpdateEvent)
        {
            var playerId = reader.ReadUInt32();
            var x = reader.ReadSingle();
            var y = reader.ReadSingle();
            UpdatePosition(playerId, x, y);
        }
        else if (packetId == PacketId.LogoutEvent)
        {
            var playerId = reader.ReadUInt32();
            if (_players.ContainsKey(playerId))
            {
                Destroy(_players[playerId]);
                _players.Remove(playerId);
            }
        }
    }

    private void SpawnOtherPlayer(uint playerId)
    {
        if (playerId == _myPlayerId)
            return;
        var newPlayer = Instantiate(otherPlayerFactory);
        newPlayer.transform.position = newPlayer.GetComponent<Rigidbody2D>().position + new Vector2(Random.Range(-5.0f, 5.0f), Random.Range(-5.0f, 5.0f));
        Debug.Log("Spawn other object " + playerId);
        _players[playerId] = newPlayer;
    }

    private void UpdatePosition(uint playerId, float x, float y)
    {
        if (playerId == _myPlayerId)
            return;

        Debug.Log("UpdatePosition " + playerId);
        _players[playerId].transform.position = new Vector2(x, y);
    }
}
