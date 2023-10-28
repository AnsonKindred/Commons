using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Un4seen.Bass;

namespace Commons.Audio
{
    internal class OpusEncoder
    {
        // Saw this recommended somewhere in opus docs
        const int MAX_ENCODED_BYTES = 4000; 

        nint opusEncoder;
        int frameSize;

        private unsafe struct EncodedData
        {
            public byte[] SafeData = new byte[MAX_ENCODED_BYTES];
            public nint DataIntPtr;
            public byte* DataRawPtr;
            GCHandle pinnedDataHandle;

            public EncodedData()
            {
                // I tried using a fixed array but it seems to get moved but explicitely pinning seems to work
                pinnedDataHandle = GCHandle.Alloc(SafeData, GCHandleType.Pinned);
                DataIntPtr = pinnedDataHandle.AddrOfPinnedObject();
                DataRawPtr = (byte*)DataIntPtr;
            }
        }

        EncodedData encodedData = new EncodedData();

        Queue<ushort> recordedSequenceNumber = new();
        Queue<long> recordedTime = new();
        object monitorLock = new();
        ushort currentSequenceNumber = 0;
        bool doMonitoring = false;
        bool isPassthrough = false;

        public OpusEncoder(int sampleRate = 48000, int numChannels = 1, int frameSize = 480)
        {
            this.frameSize = frameSize;

            opusEncoder = Opus.opus_encoder_create(sampleRate, numChannels, (int)Opus.Application.OPUS_APPLICATION_VOIP, out nint error);
        }

        public async void Monitor()
        {
            doMonitoring = true;
            using (var encoderMonitorFile = new FileStream("encoder.stuff", FileMode.OpenOrCreate, FileAccess.Write))
            using (var writer = new BinaryWriter(encoderMonitorFile))
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
                        }
                    }

                    await Task.Delay(100);
                }
            }
        }

        unsafe public bool Record(int handle, nint bytesFromMic, int numAvailableBytesFromMic, nint user)
        {
            if (isPassthrough)
            {
                return PassthroughRecord(bytesFromMic, numAvailableBytesFromMic);
            }

            int encodingOffset = 0;
            nint encodingPos = encodedData.DataIntPtr;
            nint encodedDataEndPosition = encodingPos + MAX_ENCODED_BYTES;
            nint endOfMicBytes = bytesFromMic + numAvailableBytesFromMic;
            while (bytesFromMic < endOfMicBytes)
            {
                int packetLength = Opus.opus_encode(opusEncoder, bytesFromMic, frameSize, encodingPos, (int)(encodedDataEndPosition - encodingPos) + 1);

                bool lockTaken = false;
                try
                {
                    System.Threading.Monitor.TryEnter(monitorLock, ref lockTaken);
                    if (lockTaken)
                    {
                        encodedData.SafeData[encodingOffset + packetLength] = (byte)currentSequenceNumber;
                        encodedData.SafeData[encodingOffset + packetLength + 1] = (byte)(currentSequenceNumber >> 8);

                        long time = DateTime.Now.Ticks / TimeSpan.TicksPerMicrosecond;
                        recordedSequenceNumber.Enqueue(currentSequenceNumber);
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

                AudioController.DoSomethingWithEncodedDataFromMic(encodedData.SafeData, encodingOffset, packetLength + sizeof(ushort));

                bytesFromMic += frameSize * sizeof(short);
                encodingPos += packetLength + sizeof(ushort);
                encodingOffset += packetLength + sizeof(ushort);

                currentSequenceNumber++;
            }
            return true;
        }

        private unsafe bool PassthroughRecord(nint bytesFromMic, int numAvailableBytesFromMic)
        {
            int encodingOffset = 0;
            int numBytesRead = 0;
            while (numBytesRead < numAvailableBytesFromMic)
            {
                long time = DateTime.Now.Ticks / TimeSpan.TicksPerMicrosecond;
                int packetLength = Math.Min(frameSize * 2, numAvailableBytesFromMic - numBytesRead);
                Buffer.MemoryCopy((byte*)(bytesFromMic + numBytesRead), encodedData.DataRawPtr +  encodingOffset, packetLength, packetLength);
                encodedData.SafeData[encodingOffset + packetLength] = (byte)currentSequenceNumber;
                encodedData.SafeData[encodingOffset + packetLength + 1] = (byte)(currentSequenceNumber >> 8);

                AudioController.DoSomethingWithEncodedDataFromMic(encodedData.SafeData, encodingOffset, packetLength + sizeof(ushort));

                numBytesRead += packetLength;
                encodingOffset += packetLength + sizeof(ushort);

                bool lockTaken = false;
                try
                {
                    System.Threading.Monitor.TryEnter(monitorLock, ref lockTaken);
                    if (lockTaken)
                    {
                        recordedSequenceNumber.Enqueue(currentSequenceNumber);
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

                currentSequenceNumber++;
            }

            return true;
        }

        internal void StopMonitoring()
        {
            doMonitoring = false;
        }
    }
}
