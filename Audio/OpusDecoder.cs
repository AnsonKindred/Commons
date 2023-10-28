using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Un4seen.Bass;

namespace Commons.Audio
{
    internal class OpusDecoder
    {
        nint opusDecoder;
        int frameSize;

        Queue<ushort> recordedSequenceNumber = new();
        Queue<long> recordedTime = new();
        object monitorLock = new();

        public bool doMonitoring = false;

        bool isPassthrough = false;
        ushort currentSequenceNumber;

        public OpusDecoder(int sampleRate = 48000, int numChannels = 1, int frameSize = 48000)
        {
            this.frameSize = frameSize;
            opusDecoder = Opus.opus_decoder_create(sampleRate, numChannels, out nint error);
        }

        public async void Monitor()
        {
            doMonitoring = true;
            using (var decoderMonitorFile = new FileStream("decoder.stuff", FileMode.OpenOrCreate, FileAccess.Write))
            using (var writer = new BinaryWriter(decoderMonitorFile))
            {
                while (doMonitoring)
                {
                    lock (monitorLock)
                    {
                        while (recordedSequenceNumber.Count > 0)
                        {
                            ushort sequenceNumber = recordedSequenceNumber.Dequeue();
                            long time = recordedTime.Dequeue();
                            writer.Write(sequenceNumber);
                            writer.Write(time);
                            //Trace.WriteLine("Decoded " + recordedSequenceNumber.Dequeue() + " at " + recordedTime.Dequeue());
                        }
                    }

                    await Task.Delay(100);
                }
            }
        }

        public unsafe int Stream(int handle, nint buffer, int numDesiredBytes, nint user)
        {
            if (isPassthrough)
            {
                return PassthroughStream(buffer, numDesiredBytes);
            }

            int totalPCMBytesProcessed = 0;
            while (totalPCMBytesProcessed < numDesiredBytes)
            {
                bool hasPacket = AudioController.TryGetEncodedDataForSpeaker(out var packet);
                if (!hasPacket) break;

                int numSamplesProcessed = 0;
                fixed (byte* packetBytes = packet.Array)
                {
                    numSamplesProcessed = Opus.opus_decode(opusDecoder, new nint(packetBytes + packet.Offset), packet.Count - sizeof(ushort), new nint(buffer + totalPCMBytesProcessed), frameSize, 0);
                }
                totalPCMBytesProcessed += numSamplesProcessed * sizeof(short); // This short is because pcm samples are 16 bit or something

                int sequenceNumberStartIndex = packet.Offset + packet.Count - sizeof(ushort);
                ushort sequenceNumber = packet.Array[sequenceNumberStartIndex];
                sequenceNumber += (ushort)(packet.Array[sequenceNumberStartIndex + 1] << 8);
                if (sequenceNumber != currentSequenceNumber + 1)
                {
                    Trace.WriteLine("lost a packet it seems: " + (sequenceNumber + 1));
                }
                currentSequenceNumber = sequenceNumber;

                bool lockTaken = false;
                try
                {
                    System.Threading.Monitor.TryEnter(monitorLock, ref lockTaken);
                    if (lockTaken)
                    {
                        long time = DateTime.Now.Ticks / TimeSpan.TicksPerMicrosecond;
                        recordedSequenceNumber.Enqueue(sequenceNumber);
                        recordedTime.Enqueue(time);
                    }
                }
                finally
                {
                    if (lockTaken)
                    {
                        System.Threading.Monitor.Exit(monitorLock);
                    }
                }
            }
            return totalPCMBytesProcessed;
        }

        private int PassthroughStream(nint buffer, int numDesiredBytes)
        {
            int totalPCMBytesProcessed = 0;
            while (totalPCMBytesProcessed < numDesiredBytes)
            {
                bool hasPacket = AudioController.TryGetEncodedDataForSpeaker(out var packet);
                if (!hasPacket) break;

                Marshal.Copy(packet.Array, packet.Offset, new nint(buffer + totalPCMBytesProcessed), packet.Count - sizeof(ushort));
                totalPCMBytesProcessed += packet.Count - sizeof(ushort);

                int sequenceNumberStartIndex = packet.Offset + packet.Count - 2;
                ushort sequenceNumber = packet.Array[sequenceNumberStartIndex];
                sequenceNumber += (ushort)(packet.Array[sequenceNumberStartIndex + 1] << 8);

                if (sequenceNumber != currentSequenceNumber + 1)
                {
                    Trace.WriteLine("lost a packet it seems: " + (sequenceNumber + 1));
                }
                currentSequenceNumber = sequenceNumber;

                bool lockTaken = false;
                try
                {
                    System.Threading.Monitor.TryEnter(monitorLock, ref lockTaken);
                    if (lockTaken)
                    {
                        long time = DateTime.Now.Ticks / TimeSpan.TicksPerMicrosecond;
                        recordedSequenceNumber.Enqueue(sequenceNumber);
                        recordedTime.Enqueue(time);
                    }
                }
                finally
                {
                    if (lockTaken)
                    {
                        System.Threading.Monitor.Exit(monitorLock);
                    }
                }
            }
            return totalPCMBytesProcessed;
        }

        internal void StopMonitoring()
        {
            doMonitoring = false;
        }
    }
}
