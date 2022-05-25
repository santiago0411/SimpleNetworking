using System;
using System.Net.Sockets;
using SimpleNetworking.Utils;

namespace SimpleNetworking.Server
{
    internal class ServerTcp
    {
        public TcpClient Socket { get; private set; }

        private NetworkStream stream;
        private Packet receivedData;
        private byte[] receiveBuffer;

        private readonly ServerClient serverClient;
        private readonly ServerOptions options;

        public ServerTcp(ServerClient serverClient, ServerOptions options)
        {
            this.serverClient = serverClient;
            this.options = options;
        }

        public void Connect(TcpClient socket)
        {
            try
            {
                serverClient.Logger.Debug("Connecting TCP client...");

                Socket = socket;
                Socket.ReceiveBufferSize = options.ReceiveDataBufferSize;
                Socket.SendBufferSize = options.SendDataBufferSize;
                Socket.ReceiveTimeout = options.ReceiveDataTimeout;
                Socket.SendTimeout = options.SendDataTimeout;

                stream = Socket.GetStream();

                receivedData = new Packet();
                receiveBuffer = new byte[options.ReceiveDataBufferSize];

                stream.BeginRead(receiveBuffer, 0, options.ReceiveDataBufferSize, ReceiveCallback, null);

                serverClient.Logger.Debug("Successfully connected client through TCP.");
            }
            catch (Exception ex)
            {
                serverClient.Logger.Error($"There was an error trying to establish a TCP connection to the client with id: {serverClient.Id}. The TCP socket of this client will be closed.\n{ex}");
                serverClient.Disconnect(false);
                options.NetworkOperationFailedCallback?.Invoke(serverClient.ClientInfo, FailedOperation.ConnectTcp, ex);
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

            serverClient.Logger.Info("TCP client socket has been disconnected and closed.");


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
                serverClient.Logger.Error($"There was an error trying to send TCP data to the client with id: {serverClient.Id}.\n{ex}");

                if (options.DisconnectClientOnError)
                {
                    serverClient.Logger.Info($"The client will be disconnected.");
                    serverClient.Disconnect();
                }

                options.NetworkOperationFailedCallback?.Invoke(serverClient.ClientInfo, FailedOperation.SendDataTcp, ex);
            }
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
                serverClient.Logger.Debug("New TCP data received.");

                int byteLength = stream.EndRead(result);

                if (byteLength <= 0)
                {
                    serverClient.Logger.Warn("The received TCP data contains no bytes. Disconnecting client...");
                    serverClient.Disconnect();
                    return;
                }

                serverClient.Logger.Debug("Preparing data to be processed...");
                byte[] data = new byte[byteLength];
                Array.Copy(receiveBuffer, data, byteLength);

                serverClient.Logger.Debug($"Raw data is [{BitConverter.ToString(data).Replace("-", "")}].");

                receivedData.Reset(TcpDataHandler.HandleData(serverClient.Id, data, receivedData, serverClient.Logger, serverDataReceivedCallback: options.DataReceivedCallback));
                stream.BeginRead(receiveBuffer, 0, options.ReceiveDataBufferSize, ReceiveCallback, null);
            }
            catch (System.IO.IOException)
            {
                serverClient.Logger.Info("The socket has been closed by the client.");
                serverClient.Disconnect();
            }
            catch (Exception ex)
            {
                serverClient.Logger.Error($"There was an error trying to receive TCP data from the client with id: {serverClient.Id}.\n{ex}");

                if (options.DisconnectClientOnError)
                {
                    serverClient.Logger.Info($"The client will be disconnected.");
                    serverClient.Disconnect();
                }

                options.NetworkOperationFailedCallback?.Invoke(serverClient.ClientInfo, FailedOperation.ReceiveDataTcp, ex);
            }
        }
    }
}