﻿using System;
using System.Net.Sockets;
using SimpleNetworking.Utils;

namespace SimpleNetworking.Client
{
    internal class ClientTcp
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(ClientTcp));

        public TcpClient Socket { get; private set; }

        private NetworkStream stream;
        private Packet receivedData;
        private byte[] receiveBuffer;

        private readonly Client client;

        public ClientTcp(Client client)
        {
            this.client = client;
        }

        public void Connect()
        {
            log.Debug("Setting up socket.");

            Socket = new TcpClient
            {
                ReceiveBufferSize = client.Options.ReceiveDataBufferSize,
                SendBufferSize = client.Options.SendDataBufferSize,
                ReceiveTimeout = client.Options.ReceiveDataTimeout,
                SendTimeout = client.Options.SendDataTimeout
            };

            receiveBuffer = new byte[client.Options.ReceiveDataBufferSize];

            log.Info($"Trying to connect to server: {client.Options.IPAddress} on port: {client.Options.Port}...");

            Socket.Connect(client.Options.IPAddress, client.Options.Port);
            client.IsConnected = true;

            log.Info("Successfully connected to server through TCP.");

            stream = Socket.GetStream();
            receivedData = new Packet();
            stream.BeginRead(receiveBuffer, 0, client.Options.ReceiveDataBufferSize, ReceiveCallback, null);
            log.Debug("Client ready to receive data.");
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

            client.IsConnected = false;

            log.Info("TCP client socket has been disconnected and closed.");

            if (invokeCallback)
                client.Options.ClientDisconnectedCallback?.Invoke(Protocol.Tcp);
        }

        public void SendData(Packet packet)
        {
            try
            {
                packet.WriteLength();

                if (!(Socket is null))
                    stream.BeginWrite(packet.ToArray(), 0, packet.Length(), null, null);
            }
            catch (Exception ex)
            {
                log.Error("There was an error trying to send TCP data to the server.", ex);

                if (client.Options.DisconnectClientOnError)
                {
                    log.Info($"The client will be disconnected from both protocols.");
                    client.Disconnect();
                }

                client.Options.NetworkOperationFailedCallback?.Invoke(FailedOperation.SendDataTcp, ex);
            }
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
                int byteLength = stream.EndRead(result);

                log.Debug("New TCP data received.");

                if (byteLength <= 0)
                {
                    log.Warn("The received TCP data contains no bytes. The client will be disconnected from both protocols.");
                    client.Disconnect();
                    return;
                }

                log.Debug("Preparting data to be processed...");
                byte[] data = new byte[byteLength];
                Array.Copy(receiveBuffer, data, byteLength);

                log.Debug($"Raw data is [{BitConverter.ToString(data).Replace("-", "")}].");

                receivedData.Reset(TcpDataHandler.HandleData(client.Id, data, receivedData, clientDataReceivedCallback: client.Options.DataReceivedCallback));
                stream.BeginRead(receiveBuffer, 0, client.Options.ReceiveDataBufferSize, ReceiveCallback, null);
            }
            catch (System.IO.IOException)
            {
                log.Info("The socket has been closed by the server.");
                client.Disconnect();
            }
            catch (Exception ex)
            {
                log.Error("There was an error trying to receive TCP data from the server.", ex);

                if (client.Options.DisconnectClientOnError)
                {
                    log.Info($"The client will be disconnected from both protocols.");
                    client.Disconnect();
                }

                client.Options.NetworkOperationFailedCallback?.Invoke(FailedOperation.ReceiveDataTcp, ex);
            }
        }
    }
}