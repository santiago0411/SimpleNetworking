using System;
using System.Net.Sockets;
using SimpleNetworking.Utils;

namespace SimpleNetworking.Client
{
    internal class ClientTCP
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(ClientTCP));

        public TcpClient Socket { get; private set; }

        private NetworkStream stream = null;
        private Packet receivedData = null;
        private byte[] receiveBuffer = null;

        private readonly Client client = null;

        public ClientTCP(Client client)
        {
            this.client = client;
        }

        public void Connect()
        {
            log.Debug("Setting up socket.");

            Socket = new TcpClient
            {
                ReceiveBufferSize = client.Options.ReceiveDataBufferSize,
                SendBufferSize = client.Options.SendDataBufferSize
            };

            receiveBuffer = new byte[client.Options.ReceiveDataBufferSize];

            try
            {
                log.Info($"Trying to connect to server: {client.Options.IPAddress} on port: {client.Options.Port}...");

                Socket.Connect(client.Options.IPAddress, client.Options.Port);
                client.TcpIsConnected = true;

                log.Info("Successfully connected to server through TCP.");

                stream = Socket.GetStream();
                receivedData = new Packet();
                stream.BeginRead(receiveBuffer, 0, client.Options.ReceiveDataBufferSize, ReceiveCallback, null);
                log.Debug("Client ready to receive data.");
            }
            catch
            {
                log.Error("Error connecting client to server.");
                Disconnect();
                throw;
            }
        }

        public void SendData(Packet packet)
        {
            try
            {
                packet.WriteLength();

                if (!(Socket is null))
                    stream.BeginWrite(packet.ToArray(), 0, packet.Length(), null, null);
            }
            catch
            {
                if (client.Options.DisconnectClientOnError)
                {
                    log.Error($"There was an error trying to send TCP data to the server. Disconnecting...");
                    client.Disconnect();
                }
                throw;
            }
        }

        public void Disconnect()
        {
            Socket?.Close();
            Socket?.Dispose();
            Socket = null;

            stream?.Close();
            stream?.Dispose();
            stream = null;
  
            receivedData?.Dispose();
            receivedData = null;

            receiveBuffer = null;

            client.TcpIsConnected = false;

            log.Info("TCP client socket has been disconnected and closed.");
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
                log.Debug("New TCP data received.");

                int byteLength = stream.EndRead(result);

                if (byteLength <= 0)
                {
                    log.Warn("The received TCP data contains no bytes. Disconnecting...");
                    client.Disconnect();
                    return;
                }

                log.Debug("Preparting data to be processed...");
                byte[] data = new byte[byteLength];
                Array.Copy(receiveBuffer, data, byteLength);

                log.Debug($"Raw data is {System.Text.Encoding.Default.GetString(data)}.");

                receivedData.Reset(TcpDataHandler.HandleData(client.Id, data, receivedData, client.ThreadManager, clientDataReceivedCallback: client.Options.DataReceivedCallback));
                stream.BeginRead(receiveBuffer, 0, client.Options.ReceiveDataBufferSize, ReceiveCallback, null);
            }
            catch
            {
                if (client.Options.DisconnectClientOnError)
                {
                    client.Disconnect();
                    log.Error($"There was an error trying to receive TCP data from the server. Disconnecting...");
                }
                throw;
            }
        }
    }
}