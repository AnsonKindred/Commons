using System;
using System.Reflection.Metadata.Ecma335;
using Un4seen.Bass;

namespace Commons.Audio
{
    public delegate bool PacketSource(out ArraySegment<byte> packet);

    internal class OpusDecoder
    {
        IntPtr opusDecoder;
        int frameSize;

        public STREAMPROC StreamProcess { get; private set; }

        public OpusDecoder(int sampleRate = 48000, int numChannels = 1, int frameSize = 480)
        {
            this.frameSize = frameSize;
            opusDecoder = Opus.opus_decoder_create(sampleRate, numChannels, out nint error);
            StreamProcess = new STREAMPROC(Stream);
        }

        unsafe int Stream(int handle, IntPtr buffer, int numDesiredBytes, IntPtr user)
        {
            int totalPCMBytesProcessed = 0;
            while (totalPCMBytesProcessed < numDesiredBytes)
            {
                bool hasPacket = AudioController.TryGetEncodedDataForSpeaker(out var packet);
                if (!hasPacket) break;
                int numSamplesProcessed = 0;
                fixed (byte* packetBytes = packet.Array)
                {
                    numSamplesProcessed = Opus.opus_decode(opusDecoder, new nint(packetBytes + packet.Offset), packet.Count, new nint(buffer + totalPCMBytesProcessed), frameSize, 0);
                }
                totalPCMBytesProcessed += numSamplesProcessed * sizeof(short); // This short is because pcm samples are 16 bit or something
            }
            return totalPCMBytesProcessed;
        }
    }
}
