using System;
using System.Net.Sockets;
using SimpleNetworking.Utils;

namespace SimpleNetworking.Server
{
    internal class ServerTCP
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(ServerTCP));

        public TcpClient Socket { get; private set; }

        private NetworkStream stream = null;
        private Packet receivedData = null;
        private byte[] receiveBuffer = null;

        private readonly uint id;
        private readonly int receiveBufferSize;
        private readonly int sendBufferSize;
        private readonly bool disconnectOnError;
        private readonly ThreadManager threadManager;
        private readonly Action disconnectClient;
        private readonly Action<uint, Packet> dataReceivedCallback;

        public ServerTCP(uint id, int receiveBufferSize, int sendBufferSize, bool disconnectOnError, ThreadManager threadManager, Action disconnectClient, Action<uint, Packet> dataReceivedCallback)
        {
            this.id = id;
            this.receiveBufferSize = receiveBufferSize;
            this.sendBufferSize = sendBufferSize;
            this.disconnectOnError = disconnectOnError;
            this.threadManager = threadManager;
            this.disconnectClient = disconnectClient;
            this.dataReceivedCallback = dataReceivedCallback;
        }

        public void Connect(TcpClient socket)
        {
            try
            {
                log.Info("Connecting TCP client...");

                Socket = socket;
                Socket.ReceiveBufferSize = receiveBufferSize;
                Socket.SendBufferSize = sendBufferSize;

                stream = Socket.GetStream();

                receivedData = new Packet();
                receiveBuffer = new byte[receiveBufferSize];

                stream.BeginRead(receiveBuffer, 0, receiveBufferSize, ReceiveCallback, null);

                log.Info("Successfully connected client through TCP.");
            }
            catch
            {
                if (disconnectOnError)
                {
                    disconnectClient();
                    log.Error($"There was an error trying to connect the client with id: {id} and it has been disconnected.");
                }
                throw;
            }
        }

        public void Disconnect()
        {
            if (!(Socket is null))
                Socket.Close();

            stream.Dispose();
            stream = null;
            receivedData = null;
            receiveBuffer = null;
            Socket = null;

            log.Info("TCP client socket has been disconnected and closed.");
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
                if (disconnectOnError)
                {
                    disconnectClient();
                    log.Error($"There was an error trying to send TCP data to the client with id: {id} and it has been disconnected.");
                }
                throw;
            }
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
                log.Debug("New TCP data received.");

                int byteLength = stream.EndRead(result);

                if (byteLength <= 0)
                {
                    log.Warn("The received TCP data contains no bytes. Disconnecting client...");
                    disconnectClient();
                    return;
                }

                log.Debug("Preparting data to be processed...");
                byte[] data = new byte[byteLength];
                Array.Copy(receiveBuffer, data, byteLength);

                log.Debug($"Raw data is {System.Text.Encoding.Default.GetString(data)}.");

                receivedData.Reset(TcpDataHandler.HandleData(id, data, receivedData, threadManager, serverDataReceivedCallback: dataReceivedCallback));
                stream.BeginRead(receiveBuffer, 0, receiveBufferSize, ReceiveCallback, null);
            }
            catch
            {
                if (disconnectOnError)
                {
                    disconnectClient();
                    log.Error($"There was an error trying to receive TCP data from the client with id: {id} and it has been disconnected.");
                }
                throw;
            }
        }
    }
}
