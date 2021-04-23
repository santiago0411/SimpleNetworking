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

        private readonly ServerClient serverClient;
        private readonly ServerOptions options;

        public ServerTCP(ServerClient serverClient, ServerOptions options)
        {
            this.serverClient = serverClient;
            this.options = options;
        }

        public void Connect(TcpClient socket)
        {
            try
            {
                log.Debug("Connecting TCP client...");

                Socket = socket;
                Socket.ReceiveBufferSize = options.ReceiveDataBufferSize;
                Socket.SendBufferSize = options.SendDataBufferSize;
                Socket.ReceiveTimeout = options.ReceiveDataTimeout;
                Socket.SendTimeout = options.SendDataTimeout;

                stream = Socket.GetStream();

                receivedData = new Packet();
                receiveBuffer = new byte[options.ReceiveDataBufferSize];

                stream.BeginRead(receiveBuffer, 0, options.ReceiveDataBufferSize, ReceiveCallback, null);

                log.Debug("Successfully connected client through TCP.");
            }
            catch (Exception ex)
            {
                log.Error($"There was an error trying to establish a TCP connection to the client with id: {serverClient.Id}. The TCP socket of this client will be closed.", ex);
                serverClient.Disconnect(false);
                options.NetworkOperationFailedCallback?.Invoke(serverClient.ClientInfo, FailedOperation.ConnectTCP, ex);
            }
        }

        public void Disconnect(bool invokeCallback = true)
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

            serverClient.ClientInfo.HasActiveTcpConnection = false;

            log.Info("TCP client socket has been disconnected and closed.");


            if (invokeCallback)
                options.ClientDisconnectedCallback?.Invoke(serverClient.ClientInfo, ServerProtocol.Tcp);
        }

        public void SendData(Packet packet)
        {
            try
            {
                packet.WriteLength();
                stream.BeginWrite(packet.ToArray(), 0, packet.Length(), null, null);
            }
            catch (Exception ex)
            {
                log.Error($"There was an error trying to send TCP data to the client with id: {serverClient.Id}.", ex);

                if (options.DisconnectClientOnError)
                {
                    log.Info($"The client will be disconnected.");
                    serverClient.Disconnect();
                }

                options.NetworkOperationFailedCallback?.Invoke(serverClient.ClientInfo, FailedOperation.SendDataTCP, ex);
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
                    serverClient.Disconnect();
                    return;
                }

                log.Debug("Preparting data to be processed...");
                byte[] data = new byte[byteLength];
                Array.Copy(receiveBuffer, data, byteLength);

                log.Debug($"Raw data is [{BitConverter.ToString(data).Replace("-", "")}].");

                receivedData.Reset(TcpDataHandler.HandleData(serverClient.Id, data, receivedData, serverDataReceivedCallback: options.DataReceivedCallback));
                stream.BeginRead(receiveBuffer, 0, options.ReceiveDataBufferSize, ReceiveCallback, null);
            }
            catch (System.IO.IOException)
            {
                log.Info("The socket has been closed by the client.");
                serverClient.Disconnect();
            }
            catch (Exception ex)
            {
                log.Error($"There was an error trying to receive TCP data from the client with id: {serverClient.Id}.", ex);

                if (options.DisconnectClientOnError)
                {
                    log.Info($"The client will be disconnected.");
                    serverClient.Disconnect();
                }

                options.NetworkOperationFailedCallback?.Invoke(serverClient.ClientInfo, FailedOperation.ReceiveDataTCP, ex);
            }
        }
    }
}