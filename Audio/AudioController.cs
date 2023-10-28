using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Fx;

namespace Commons.Audio
{
    internal static class AudioController
    {
        const int SAMPLE_RATE = 48000;
        const int NUM_CHANNELS = 1;

        private static int micInputChannel = 0;
        private static int speakerOutputChannel = 0;

        public static OpusEncoder opusEncoder;
        public static OpusDecoder opusDecoder;

        static ArraySegment<byte>[] packetBuffer = new ArraySegment<byte>[1024];
        static int packetBufferReadIndex = 0;
        static int packetBufferWriteIndex = 0;

        //static Queue<Memory<byte>> packetQueue = new Queue<Memory<byte>>();

        static int pluginFX;
        static int fxCompressorHandle;
        static BASS_BFX_COMPRESSOR2 compressorSettings;

        static VoipPeer? voipPeer;

        const int MIC_BUFFER_MS = 10;

        static STREAMPROC StreamProcess;
        static RECORDPROC RecordingProcess;

        static AudioController()
        {
            opusEncoder = new OpusEncoder();
            opusDecoder = new OpusDecoder();
            compressorSettings = new BASS_BFX_COMPRESSOR2(-2.5f, -24, 2, .02f, 20, BASSFXChan.BASS_BFX_CHANALL);

            RecordingProcess = new RECORDPROC(opusEncoder.Record);
            StreamProcess = new STREAMPROC(opusDecoder.Stream);
        }

        public static void Init()
        {
            pluginFX = Bass.BASS_PluginLoad("bass_fx.dll");
            BassFx.BASS_FX_GetVersion();
            Bass.BASS_Init(-1, SAMPLE_RATE, BASSInit.BASS_DEVICE_MONO, IntPtr.Zero);
            Bass.BASS_RecordInit(-1);

            //StartMicInput();
            //StartSpeakerOutput();
        }

        public static bool PassthroughRecord(int handle, nint bytesFromMic, int numAvailableBytesFromMic, nint user)
        {
            
            return true;
        }

        unsafe public static void DoSomethingWithEncodedDataFromMic(byte[] data, int offset, int length)
        {
            voipPeer?.Send(data, offset, length);
        }

        public static bool TryGetEncodedDataForSpeaker(out ArraySegment<byte> packet)
        {
            if (packetBufferReadIndex == packetBufferWriteIndex)
            {
                packet = new ArraySegment<byte>();
                return false;
            }
            else
            {
                packet = packetBuffer[packetBufferReadIndex];
                packetBufferReadIndex++;
                if (packetBufferReadIndex >= packetBuffer.Length)
                {
                    packetBufferReadIndex = 0;
                }
                return true;
            }
        }

        public static void AddAudioSamples(ArraySegment<byte> data)
        {
            packetBuffer[packetBufferWriteIndex] = data;
            packetBufferWriteIndex++;
            if (packetBufferWriteIndex >= packetBuffer.Length)
            {
                packetBufferWriteIndex = 0;
            }
        }

        public static void StartMicInput()
        {
            micInputChannel = Bass.BASS_RecordStart(SAMPLE_RATE, NUM_CHANNELS, BASSFlag.BASS_DEFAULT, MIC_BUFFER_MS, RecordingProcess, nint.Zero);
            fxCompressorHandle = Bass.BASS_ChannelSetFX(micInputChannel, BASSFXType.BASS_FX_BFX_COMPRESSOR2, 0);
            Bass.BASS_FXSetParameters(fxCompressorHandle, compressorSettings);
        }

        public static void StartSpeakerOutput()
        {
            speakerOutputChannel = Bass.BASS_StreamCreate(SAMPLE_RATE, NUM_CHANNELS, BASSFlag.BASS_DEFAULT, StreamProcess, 0);
            Bass.BASS_ChannelPlay(speakerOutputChannel, false);
        }

        internal static void SetVoipPeer(VoipPeer voipPeer)
        {
            AudioController.voipPeer = voipPeer;
        }

        internal static void StartMonitoringEncoding()
        {
            opusEncoder.Monitor();
        }

        internal static void StartMonitoringDecoding()
        {
            opusDecoder.Monitor();
        }

        internal static void StopMonitoringEncoding()
        {
            opusEncoder.StopMonitoring();
        }

        internal static void StopMonitoringDecoding()
        {
            opusDecoder.StopMonitoring();
        }
    }
}
