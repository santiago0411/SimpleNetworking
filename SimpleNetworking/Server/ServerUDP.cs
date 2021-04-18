using System;
using System.Net;
using System.Net.Sockets;
using SimpleNetworking.Utils;

namespace SimpleNetworking.Server
{
    internal class ServerUDP
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(ServerUDP));

        public IPEndPoint EndPoint { get; private set; }

        private readonly uint id;
        private readonly bool disconnectOnError;
        private readonly UdpClient udpListener;
        private readonly ThreadManager threadManager;
        private readonly Action disconnectClient;
        private readonly Action<uint, Packet> dataReceivedCallback;

        public ServerUDP(uint id, bool disconnectOnError, UdpClient udpListener, ThreadManager threadManager, Action disconnectClient, Action<uint, Packet> dataReceivedCallback)
        {
            this.id = id;
            this.disconnectOnError = disconnectOnError;
            this.udpListener = udpListener;
            this.threadManager = threadManager;
            this.disconnectClient = disconnectClient;
            this.dataReceivedCallback = dataReceivedCallback;
        }

        public void Connect(IPEndPoint endPoint)
        {
            EndPoint = endPoint;
            log.Info("Successfully connected client through UDP.");
        }

        public void Disconnect()
        {
            EndPoint = null;
            log.Info("UDP client endpoint has been disconnected and closed.");
        }

        public void SendData(Packet packet)
        {
            try
            {
                packet.WriteLength();

                if (!(EndPoint is null))
                    udpListener.BeginSend(packet.ToArray(), packet.Length(), EndPoint, null, null);
            }
            catch
            {
                if (disconnectOnError)
                {
                    disconnectClient();
                    log.Error($"There was an error trying to send UDP data to the client with id: {id} and it has been disconnected.");
                }
                throw;
            }
        }

        public void HandleData(Packet packetData)
        {
            log.Debug("Processing the received UDP data.");

            if (packetData.UnreadLength() < 4)
            {
                log.Warn($"The received UDP data contains no bytes besides the client id: {id}.");
                return;
            }

            int packetLength = packetData.ReadInt();
            byte[] packetBytes = packetData.ReadBytes(packetLength);
            log.Debug("Creating new packet with the received UDP data and calling DataReceivedCallback.");

            threadManager.ExecuteOnMainThread(() =>
            {
                using var packet = new Packet(packetBytes);
                dataReceivedCallback(id, packet);
            });
        }
    }
}
