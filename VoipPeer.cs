using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Commons.Audio;
using NobleConnect.Ice;

namespace Commons
{
    internal class VoipPeer : Peer
    {
        public VoipPeer() : base(445)
        {
        }

        protected override async void ReceiveFromClient(TcpClient client)
        {
            try
            {
                // Get a stream object for reading and writing
                NetworkStream stream = client.GetStream();

                // Loop to receive all the data sent by the client.
                while (client.Connected)
                {
                    // Read the byte that represents the audio
                    byte[] audio = new byte[bufferSize];
                    int numBytesRead = await stream.ReadAsync(audio, 0, audio.Length);
                    AudioController.AddAudioSamples(audio, numBytesRead);
                }
            }
            catch (ObjectDisposedException) { }
            catch (IOException) { }

            connectedClients.Remove(client.GetStream());
        }

        internal void Send(ArraySegment<byte> data)
        {
            if (localClientStream != null && localClientStream.CanWrite) 
            {
                localClientStream.WriteAsync(data);
            }

            if (connectedClients.Count > 0)
            {
                foreach (var client in connectedClients)
                {
                    if (client != null && client.CanWrite)
                    {
                        client.WriteAsync(data);
                    }
                }
            }
        }
    }
}
