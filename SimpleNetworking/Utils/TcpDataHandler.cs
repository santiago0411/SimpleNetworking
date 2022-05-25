using System;

namespace SimpleNetworking.Utils
{
    internal class TcpDataHandler
    {
        public static bool HandleData(int clientId, byte[] data, Packet receivedData, InternalLogger logger, Action<int, Packet> serverDataReceivedCallback = null, Action<Packet> clientDataReceivedCallback = null)
        {
            logger.Debug("Processing new TCP data...");
            receivedData.SetBytes(data);

            if (receivedData.UnreadLength() < 4)
            {
                logger.Warn("Failed to read packet length, data does not have enough bytes. Packet will not be processed.");
                return true;
            }

            int packetLength = receivedData.ReadInt();
            logger.Debug($"Packet length is: {packetLength}.");

            if (packetLength <= 0) return true;

            while (packetLength > 0 && packetLength <= receivedData.UnreadLength())
            {
                byte[] packetBytes = receivedData.ReadBytes(packetLength);

                logger.Debug("Creating new packet with the received TCP data and calling DataReceivedCallback.");

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