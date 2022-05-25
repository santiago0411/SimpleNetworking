using System;
using System.Net;
using System.Net.Sockets;
using SimpleNetworking.Utils;

namespace SimpleNetworking.Client
{
    internal class ClientUdp
    {
        public UdpClient Socket { get; private set; }

        private IPEndPoint endPoint;

        private readonly Client client;

        public ClientUdp(Client client)
        {
            this.client = client;
            endPoint = new IPEndPoint(IPAddress.Parse(client.Options.IPAddress), client.Options.Port);
        }

        public void Connect()
        {
            client.Logger.Info("Connecting UDP client...");

            Socket = new UdpClient();
            Socket.Connect(endPoint);
            Socket.BeginReceive(ReceiveCallback, null);

            client.Logger.Info("Successfully connected to server through UDP.");
        }

        public void Disconnect(bool invokeCallback = true)
        {
            Socket?.Close();
            Socket?.Dispose();
            Socket = null;
            endPoint = null;
            client.Logger.Info("UDP client socket has been disconnected and closed.");

            if (invokeCallback)
                client.Options.ClientDisconnectedCallback?.Invoke(Protocol.Udp);
        }

        public void SendData(Packet packet, bool writeId)
        {
            try
            {
                packet.WriteLength();

                if (writeId)
                {
                    if (client.Id == 0)
                        client.Logger.Warn("The id has not been set and its value is still 0.");

                    packet.InsertInt(client.Id);
                }

                if (!(Socket is null))
                    Socket.BeginSend(packet.ToArray(), packet.Length(), null, null);
            }
            catch (Exception ex)
            {
                client.Logger.Error($"There was an error trying to send UDP data to the server.\n{ex}");
                client.Logger.Info($"The UDP socket will be closed.");
                Disconnect();

                client.Options.NetworkOperationFailedCallback?.Invoke(FailedOperation.SendDataUdp, ex);
            }
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
                byte[] data = Socket.EndReceive(result, ref endPoint);
                Socket.BeginReceive(ReceiveCallback, null);

                client.Logger.Debug("New UDP data received.");

                if (data.Length < 4)
                {
                    client.Logger.Warn("The UDP data received did not contain enough bytes to read the packet length.");
                    return;
                }

                HandleData(data);
            }
            catch (Exception ex)
            {
                client.Logger.Error($"There was an error trying to receive UDP data from the server.\n{ex}");
                client.Logger.Info($"The UDP socket will be closed.");
                Disconnect();

                client.Options.NetworkOperationFailedCallback?.Invoke(FailedOperation.SendDataUdp, ex);
            }
        }

        private void HandleData(byte[] data)
        {
            client.Logger.Debug("Processing the received UDP data.");

            using var packet = new Packet(data);
            int packetLength = packet.ReadInt();
            data = packet.ReadBytes(packetLength);

            client.Logger.Debug("Creating new packet with the received UDP data and calling DataReceivedCallback.");

            using var p = new Packet(data);
            client.Options.DataReceivedCallback(p);
        }
    }
}
