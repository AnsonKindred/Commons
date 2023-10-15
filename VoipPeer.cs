using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using NAudio;
using NAudio.Wave;

namespace Commons
{
    internal class VoipPeer : Peer
    {
        private AudioController audioController;

        public VoipPeer(AudioController audioController) 
        { 
            this.audioController = audioController;
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
                    byte[] audio = new byte[441];
                    int numBytesRead = await stream.ReadAsync(audio, 0, audio.Length);
                    audioController.AddAudioSamples(audio, numBytesRead);
                }
            }
            catch (ObjectDisposedException) { }
            catch (IOException) { }

            connectedClients.Remove(client);
        }

        internal void Send(ArraySegment<byte> data)
        {
            if (localClient == null || !localClient.Connected || localClientStream == null || !localClientStream.CanWrite) return;

            localClientStream.WriteAsync(data);
        }
    }
}
