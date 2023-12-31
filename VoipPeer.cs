﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Commons.Audio;

namespace Commons
{
    using IPTuple = Tuple<IPEndPoint, IPEndPoint>;
    internal class VoipPeer : Peer
    {
        Socket udpClient;

        struct AudioBuffer
        {
            public int Count => BackingBuffer.Length - Offset;
            public byte[] BackingBuffer;
            public int Offset;

            public AudioBuffer(int length)
            {
                BackingBuffer = new byte[length];
            }
        }

        AudioBuffer audioBuffer;

        SocketAsyncEventArgs receiveArg = new SocketAsyncEventArgs();
        SocketAsyncEventArgs sendArg = new SocketAsyncEventArgs();

        List<IPEndPoint> connectedClients = new List<IPEndPoint>();

        public bool IsHost { get; internal set; } = false;

        public VoipPeer(int audioBufferSize = short.MaxValue*2, int maxClients = 10) : base(1024, ProtocolType.Udp)
        {
            audioBuffer = new AudioBuffer(audioBufferSize * maxClients);

            udpClient = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
            udpClient.DualMode = true;
        }

        internal override async Task<IPEndPoint> StartHosting(IPEndPoint localEndPoint)
        {
            IsHost = true;
            udpClient.Bind(localEndPoint);
            if (udpClient.LocalEndPoint == null) throw new SocketException((int)SocketError.SocketError);

            receiveArg.SetBuffer(audioBuffer.BackingBuffer, audioBuffer.Offset, audioBuffer.Count);
            receiveArg.Completed += Receive;
            // This just allocates space for the remote endpoint
            receiveArg.RemoteEndPoint = new IPEndPoint(udpClient.LocalEndPoint.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any, 0);
            bool willAsync = true;
            try
            {
                willAsync = udpClient.ReceiveFromAsync(receiveArg);
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
            }
            if (!willAsync)
            {
                Receive(null, receiveArg);
            }

            return await base.StartHosting((IPEndPoint)udpClient.LocalEndPoint);
        }

        internal async override Task<IPTuple?> Connect(IPEndPoint remoteEndPoint)
        {
            if (udpClient == null) throw new Exception("Where did the udp client go?");

            IPTuple? bridgeEndPoints = await base.Connect(remoteEndPoint);
            udpClient.Bind(new IPEndPoint(IPAddress.IPv6Any, 0));

            if (udpClient.LocalEndPoint == null) throw new Exception("Socket bind failed");

            noblePeer.SetLocalEndPoint((IPEndPoint)udpClient.LocalEndPoint);

            if (bridgeEndPoints != null)
            {
                connectedClients.Add(bridgeEndPoints.Item1.AddressFamily == udpClient.LocalEndPoint.AddressFamily ? bridgeEndPoints.Item1 : bridgeEndPoints.Item2);
            }

            AudioController.StartMicInput();
            AudioController.StartSpeakerOutput();

            receiveArg.SetBuffer(audioBuffer.BackingBuffer, audioBuffer.Offset, audioBuffer.Count);
            receiveArg.Completed += Receive;
            // This just allocates space for the remote endpoint
            receiveArg.RemoteEndPoint = new IPEndPoint(udpClient.LocalEndPoint.AddressFamily == AddressFamily.InterNetwork ? IPAddress.Any : IPAddress.IPv6Any, 0);
            bool willAsync = true;
            try
            {
                willAsync = udpClient.ReceiveFromAsync(receiveArg);
            }
            catch (Exception e)
            {
                Trace.WriteLine(e.Message);
            }
            if (!willAsync)
            {
                Receive(null, receiveArg);
            }

            return bridgeEndPoints;
        }

        protected void Receive(object? sender, SocketAsyncEventArgs receiveArg)
        {
            if (receiveArg.RemoteEndPoint == null) throw new SocketException((int)SocketError.SocketError);
            if (receiveArg.BytesTransferred == 0) return;
            try
            {
                if (!connectedClients.Contains(receiveArg.RemoteEndPoint))
                {
                    Trace.WriteLine("Adding client: " + receiveArg.RemoteEndPoint);

                    if (IsHost)
                    {
                        AudioController.StartMicInput();
                        AudioController.StartSpeakerOutput();

                        // Start monitoring encoding when a client connects that way we don't have a bunch of encoded packets from before a client connected.
                        if (!AudioController.opusDecoder.doMonitoring)
                        {
                            AudioController.StartMonitoringEncoding();
                        }
                    }

                    connectedClients.Add((IPEndPoint)receiveArg.RemoteEndPoint);
                }

                AudioController.AddAudioSamples(new ArraySegment<byte>(audioBuffer.BackingBuffer, audioBuffer.Offset, receiveArg.BytesTransferred));

                // Poor man's circle buffer
                audioBuffer.Offset += receiveArg.BytesTransferred;
                if (audioBuffer.Offset + 4000 > audioBuffer.BackingBuffer.Length)
                {
                    // Not enough room for another opus packet, start back at the beginning
                    audioBuffer.Offset = 0;
                }

                receiveArg.SetBuffer(audioBuffer.BackingBuffer, audioBuffer.Offset, audioBuffer.Count);

                bool willAsync = udpClient.ReceiveFromAsync(receiveArg);
                if (!willAsync)
                {
                    Receive(sender, receiveArg);
                }
            }
            catch (ObjectDisposedException) { }
            catch (IOException) { }
        }

        internal void Send(byte[] data, int offset, int length)
        {
            if (connectedClients.Count > 0)
            {
                foreach (var client in connectedClients)
                {
                    sendArg.RemoteEndPoint = client;
                    udpClient.SendTo(data, offset, length, SocketFlags.None, client);
                }
            }
        }
    }
}
