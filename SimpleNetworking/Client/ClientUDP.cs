using System;
using System.Net;
using System.Net.Sockets;
using SimpleNetworking.Utils;

namespace SimpleNetworking.Client
{
    internal class ClientUDP
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(ClientUDP));

        public UdpClient Socket { get; private set; }

        private IPEndPoint endPoint = null;

        private readonly Client client = null;

        public ClientUDP(Client client)
        {
            this.client = client;
            endPoint = new IPEndPoint(IPAddress.Parse(client.Options.IPAddress), client.Options.Port);
        }

        public void Connect(int localPort)
        {
            try
            {
                log.Info("Connecting UDP client...");

                Socket = new UdpClient(localPort);
                Socket.Connect(endPoint);
                Socket.BeginReceive(ReceiveCallback, null);

                client.UdpIsConnected = true;

                log.Info("Successfully connected to server through UDP.");

                using var packet = new Packet();
                SendData(packet);
            }
            catch
            {
                Disconnect();
                throw;
            }
        }

        public void Disconnect()
        {
            Socket?.Close();
            Socket?.Dispose();
            Socket = null;
            endPoint = null;
            client.UdpIsConnected = false;
            log.Info("UDP client socket has been disconnected and closed.");
        }

        public void SendData(Packet packet)
        {
            try
            {
                packet.InsertInt((int)client.Id);
                packet.WriteLength();

                if (!(Socket is null))
                    Socket.BeginSend(packet.ToArray(), packet.Length(), null, null);
            }
            catch
            {
                if (client.Options.DisconnectClientOnError)
                {
                    log.Error($"There was an error trying to send UDP data to the server. Disconnecting...");
                    client.Disconnect();
                }
                throw;
            }
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
                log.Debug("New UDP data received.");

                byte[] data = Socket.EndReceive(result, ref endPoint);
                Socket.BeginReceive(ReceiveCallback, null);

                if (data.Length < 4)
                {
                    log.Warn("The UDP data received did not contain enough bytes to read the packet length.");
                    return;
                }

                HandleData(data);
            }
            catch
            {
                if (client.Options.DisconnectClientOnError)
                {
                    client.Disconnect();
                    log.Error($"There was an error trying to receive UDP data from the server. Disconnecting...");
                }
                throw;
            }
        }

        public void HandleData(byte[] data)
        {
            log.Debug("Processing the received UDP data.");

            using var packet = new Packet(data);
            int packetLength = packet.ReadInt();
            data = packet.ReadBytes(packetLength);

            log.Debug("Creating new packet with the received UDP data and calling DataReceivedCallback.");

            client.ThreadManager.ExecuteOnMainThread(() =>
            {
                using var packet = new Packet(data);
                client.Options.DataReceivedCallback(packet);
            });
        }
    }
}
