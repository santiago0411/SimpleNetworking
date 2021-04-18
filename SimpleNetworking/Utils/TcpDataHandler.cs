using System;

namespace SimpleNetworking.Utils
{
    internal class TcpDataHandler
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(TcpDataHandler));

        public static bool HandleData(uint clientId, byte[] data, Packet receivedData, ThreadManager threadManager, Action<uint, Packet> serverDataReceivedCallback = null, Action<Packet> clientDataReceivedCallback = null)
        {
            log.Debug("Processing new TCP data...");

            int packetLength = 0;
            receivedData.SetBytes(data);

            if (receivedData.UnreadLength() >= 4)
            {
                log.Debug("Reading packet length.");
                packetLength = receivedData.ReadInt();
                log.Debug($"Packet length is: {packetLength}");
                if (packetLength <= 0) return true;
            }

            while (packetLength > 0 && packetLength <= receivedData.UnreadLength())
            {
                byte[] packetBytes = receivedData.ReadBytes(packetLength);

                log.Debug("Creating new packet with the received TCP data and calling DataReceivedCallback.");
                threadManager.ExecuteOnMainThread(() =>
                {
                    using var packet = new Packet(packetBytes);
                    serverDataReceivedCallback?.Invoke(clientId, packet);
                    clientDataReceivedCallback?.Invoke(packet);
                });
            }

            packetLength = 0;

            if (receivedData.UnreadLength() >= 4)
            {
                packetLength = receivedData.ReadInt();
                if (packetLength <= 0) return true;
            }

            //TODO check
            if (packetLength <= 1) return true;

            return false;
        }
    }
}
