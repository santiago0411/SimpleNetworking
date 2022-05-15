using System;

namespace SimpleNetworking.Utils
{
    internal class TcpDataHandler
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(TcpDataHandler));

        public static bool HandleData(int clientId, byte[] data, Packet receivedData, Action<int, Packet> serverDataReceivedCallback = null, Action<Packet> clientDataReceivedCallback = null)
        {
            log.Debug("Processing new TCP data...");
            receivedData.SetBytes(data);

            if (receivedData.UnreadLength() < 4)
            {
                log.Warn("Failed to read packet length, data does not have enough bytes. Packet will not be processed.");
                return true;
            }

            int packetLength = receivedData.ReadInt();
            log.Debug($"Packet length is: {packetLength}.");

            if (packetLength <= 0) return true;

            while (packetLength > 0 && packetLength <= receivedData.UnreadLength())
            {
                byte[] packetBytes = receivedData.ReadBytes(packetLength);

                log.Debug("Creating new packet with the received TCP data and calling DataReceivedCallback.");

                using var packet = new Packet(packetBytes);
                serverDataReceivedCallback?.Invoke(clientId, packet);
                clientDataReceivedCallback?.Invoke(packet);

                packetLength = 0;

                if (receivedData.UnreadLength() >= 4)
                {
                    packetLength = receivedData.ReadInt();
                    if (packetLength <= 0) 
                        return true;
                }
            }

            return packetLength == 0;
        }
    }
}