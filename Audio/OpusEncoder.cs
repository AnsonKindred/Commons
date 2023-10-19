using System;
using System.Text;
using Un4seen.Bass;

namespace Commons.Audio
{
    delegate void PacketDestination(nint packet, int packetLength);

    unsafe internal class OpusEncoder
    {
        // Saw this recommended somewhere in opus docs
        const int MAX_ENCODED_BYTES = 4000; 

        nint opusEncoder;
        int frameSize;

        struct EncodedData
        {
            public fixed byte data[MAX_ENCODED_BYTES];
        }

        EncodedData encodedData;
        nint encodedDataStartPosition;
        nint encodedDataEndPosition;

        public RECORDPROC RecordingProcess { get; private set; }

        unsafe public OpusEncoder(int sampleRate = 48000, int numChannels = 1, int frameSize = 480)
        {
            this.frameSize = frameSize;
            fixed (byte* encoded = encodedData.data)
            {
                encodedDataStartPosition = new nint(encoded);
                encodedDataEndPosition = encodedDataStartPosition + MAX_ENCODED_BYTES;
            }
            opusEncoder = Opus.opus_encoder_create(sampleRate, numChannels, (int)Opus.Application.OPUS_APPLICATION_VOIP, out nint error);
            RecordingProcess = new RECORDPROC(Record);
        }

        unsafe public bool Record(int handle, nint bytesFromMic, int numAvailableBytesFromMic, nint user)
        {
            nint encodingPos = encodedDataStartPosition;
            nint endOfMicBytes = bytesFromMic + numAvailableBytesFromMic;
            while (bytesFromMic < endOfMicBytes)
            {
                int packetLength = Opus.opus_encode(opusEncoder, bytesFromMic, frameSize, encodingPos, (int)(encodedDataEndPosition - encodingPos));

                AudioController.DoSomethingWithEncodedDataFromMic(encodingPos, packetLength);

                bytesFromMic += frameSize * sizeof(short);
                encodingPos += packetLength;
            }
            return true;
        }
    }
}
