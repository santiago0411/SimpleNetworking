﻿using System;
using System.Net;
using System.Net.Sockets;
using SimpleNetworking.Utils;

namespace SimpleNetworking.Client
{
    internal class ClientUDP
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(ClientUDP));

        public UdpClient Socket { get; private set; }

        private IPEndPoint endPoint = null;

        private readonly Client client = null;

        public ClientUDP(Client client)
        {
            this.client = client;
            endPoint = new IPEndPoint(IPAddress.Parse(client.Options.IPAddress), client.Options.Port);
        }

        public void Connect()
        {
            log.Info("Connecting UDP client...");

            Socket = new UdpClient();
            Socket.Connect(endPoint);
            Socket.BeginReceive(ReceiveCallback, null);

            log.Info("Successfully connected to server through UDP.");
        }

        public void Disconnect(bool invokeCallback = true)
        {
            Socket?.Close();
            Socket?.Dispose();
            Socket = null;
            endPoint = null;
            log.Info("UDP client socket has been disconnected and closed.");

            if (invokeCallback)
                client.Options.ClientDisconnectedCallback?.Invoke(Protocol.Udp);
        }

        public void SendData(Packet packet, bool writeId)
        {
            try
            {
                packet.WriteLength();

                if (writeId)
                {
                    if (client.Id == 0)
                        log.Warn("The id has not been set and its value is still 0.");

                    packet.InsertInt((int)client.Id);
                }

                if (Socket is null)
                {
                    log.Warn("The UDP socket is null. Data cannot be sent.");
                    return;
                }

                Socket.BeginSend(packet.ToArray(), packet.Length(), null, null);
            }
            catch (Exception ex)
            {
                log.Error("There was an error trying to send UDP data to the server.", ex);
                log.Info($"The UDP socket will be closed.");
                Disconnect();

                client.Options.NetworkOperationFailedCallback?.Invoke(FailedOperation.SendDataUDP, ex);
            }
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
                byte[] data = Socket.EndReceive(result, ref endPoint);
                Socket.BeginReceive(ReceiveCallback, null);

                log.Debug("New UDP data received.");

                if (data.Length < 4)
                {
                    log.Warn("The UDP data received did not contain enough bytes to read the packet length.");
                    return;
                }

                HandleData(data);
            }
            catch (Exception ex)
            {
                log.Error("There was an error trying to receive UDP data from the server.", ex);
                log.Info($"The UDP socket will be closed.");
                Disconnect();

                client.Options.NetworkOperationFailedCallback?.Invoke(FailedOperation.SendDataUDP, ex);
            }
        }

        private void HandleData(byte[] data)
        {
            log.Debug("Processing the received UDP data.");

            using var packet = new Packet(data);
            int packetLength = packet.ReadInt();
            data = packet.ReadBytes(packetLength);

            log.Debug("Creating new packet with the received UDP data and calling DataReceivedCallback.");

            client.ThreadManager.ExecuteOnMainThread(() =>
            {
                using var packet = new Packet(data);
                client.Options.DataReceivedCallback(packet);
            });
        }
    }
}
