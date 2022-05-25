# Simple Networking

A simple server-client(s) TCP and UDP implementations to send and receive any type of messages. Based on my network implementation for my 2D RPG Game Project.

## Server Usage

```c#
var server = new Server(serverOptions);

try
{
    server.Listen();
}
finally
{
    server.Stop();
}
```

Upon calling **Listen()** the **Server** class will automatically start a new thread in which it will do it's operations.
Once a packet is received the callback function passed as part of the **ServerOptions** will be invoked.

## Client Usage

```c#
var client = new Client(clientOptions);

try
{
    client.ConnectToServerTcp();
    client.ConnectToServerUdp();
}
finally
{
    client.Disconnect();
}
```

## Callback Functions Note

**IMPORTANT: The callback functions for both clients and servers will be executed on the internal thread. If you wish to execute actions on the main thread, you will have to do it manually with a queue after receiving such callbacks.**

## Packet Sending

The **Packet** class **Write()** function contains overloads for writing all primitive types and also strings. *Strings will be written as UTF8 bytes. First the amount of the bytes will be written and then the bytes themselves.*
```c#
using var packet = new Packet();
packet.Write(1);
packet.Write("Hello World");
packet.Write(new Vector2(0.5f, 1.3f)); // Extension method
serverOrClient.SendPacketTcp(packet);
```

## Packet Receiving

Receiving packets works the same way for both server and client but the function signature is different. The server will receieve information about the client that sent the packet as well as the packet. Whereas the client will just receive the packet.

**The data *MUST* be read in the exact same order as it was written.**

```c#
# SERVER
public static void OnDataReceived(ClientInfo client, Packet packet)
# CLIENT
public static void OnDataReceived(Packet packet)
{
    // Reading a packet like the one above
    var packetId = packet.ReadInt();
    var message = packet.ReadString();
    var vector2 = packet.ReadVector2(); // Extension method
}
```

# Packets with custom data types

For writing or reading any other custom data types or using a different encoding for strings you can write your own extension methods for the **Packet** class like the following.

```c#
public static void Write(this Packet packet, Vector2 v2)
{
    packet.Write(v2.X);
    packet.Write(v2.Y);
}

public static Vector2 ReadVector2(this Packet packet)
{
    return new Vector2(packet.ReadFloat(), packet.ReadFloat());
}
```