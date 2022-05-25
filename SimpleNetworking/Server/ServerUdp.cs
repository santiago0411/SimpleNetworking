using System;
using System.Net;
using System.Net.Sockets;
using SimpleNetworking.Utils;

namespace SimpleNetworking.Server
{
    internal class ServerUdp
    {
        public IPEndPoint EndPoint { get; private set; }

        private readonly ServerClient serverClient;
        private readonly ServerOptions options;
        private readonly UdpClient udpListener;

        public ServerUdp(ServerClient serverClient, ServerOptions options, UdpClient udpListener)
        {
            this.serverClient = serverClient;
            this.options = options;
            this.udpListener = udpListener;
        }

        public void Connect(IPEndPoint clientEndpoint)
        {
            EndPoint = clientEndpoint;
        }

        public void Disconnect(bool invokeCallback = true)
        {
            EndPoint = null;
            serverClient.ClientInfo.HasActiveUdpConnection = false;
            serverClient.Logger.Info("UDP EndPoint disconnected.");

            if (invokeCallback)
                options.ClientDisconnectedCallback?.Invoke(serverClient.ClientInfo, ServerProtocol.Udp);
        }

        public void SendData(Packet packet)
        {
            try
            {
                packet.WriteLength();

                if (!(EndPoint is null))
                    udpListener.BeginSend(packet.ToArray(), packet.Length(), EndPoint, null, null);
            }
            catch (Exception ex)
            {
                serverClient.Logger.Error($"There was an error trying to send UDP data to the client with id: {serverClient.Id}.\n{ex}");
                options.NetworkOperationFailedCallback?.Invoke(serverClient.ClientInfo, FailedOperation.SendDataUdp, ex);
            }
        }

        public void HandleData(Packet receivedData)
        {
            serverClient.Logger.Debug($"Received new UDP data from client: {serverClient.Id}.");

            int packetLength = receivedData.ReadInt();
            serverClient.Logger.Debug($"Packet length is: {packetLength}");

            if (packetLength <= 0) return;

            byte[] packetBytes = receivedData.ReadBytes(packetLength);

            serverClient.Logger.Debug($"Raw data is [{BitConverter.ToString(packetBytes).Replace("-", "")}].");

            serverClient.Logger.Debug("Creating new packet with the received UDP data and calling DataReceivedCallback.");

            using var packet = new Packet(packetBytes);
            options.DataReceivedCallback(serverClient.Id, packet);
        }
    }
}