using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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

        static OpusEncoder opusEncoder;
        static OpusDecoder opusDecoder;

        //static UnmanagedMemoryManager<byte>? memoryManager;
        static public byte[] managedBuffer = new byte[SAMPLE_RATE * NUM_CHANNELS * sizeof(short)];
        static public int managedBufferWriteIndex = 0;

        static Queue<ArraySegment<byte>> packetBuffer = new Queue<ArraySegment<byte>>();
        static object packetBufferLock = new object();

        static int pluginFX;
        static int fxCompressorHandle;
        static BASS_BFX_COMPRESSOR2 compressorSettings;

        static VoipPeer? voipPeer;

        const int MIC_BUFFER_MS = 10;

        static AudioController()
        {
            opusEncoder = new OpusEncoder();
            opusDecoder = new OpusDecoder();
            compressorSettings = new BASS_BFX_COMPRESSOR2(-2.5f, -24, 2, .02f, 20, BASSFXChan.BASS_BFX_CHANALL);
        }

        public static void Init()
        {
            pluginFX = Bass.BASS_PluginLoad("bass_fx.dll");
            BassFx.BASS_FX_GetVersion();
            Bass.BASS_Init(-1, SAMPLE_RATE, BASSInit.BASS_DEVICE_MONO, IntPtr.Zero);
            Bass.BASS_RecordInit(-1);

            StartMicInput();
            StartSpeakerOutput();
        }

        unsafe public static void DoSomethingWithEncodedDataFromMic(nint packet, int packetLength)
        {
            if (voipPeer != null)
            {
                if (managedBufferWriteIndex + packetLength > managedBuffer.Length)
                {
                    managedBufferWriteIndex = 0;
                }
                Marshal.Copy(packet, managedBuffer, managedBufferWriteIndex, packetLength);
                
                voipPeer.Send(new ArraySegment<byte>(managedBuffer, managedBufferWriteIndex, packetLength));
                managedBufferWriteIndex += packetLength;
                //if (memoryManager == null)
                //{
                //    // This is almost certainly illegal and wrong
                //    memoryManager = new UnmanagedMemoryManager<byte>((byte*)packet, MIC_BUFFER_MS * SAMPLE_RATE * sizeof(short) / 1000);
                //}
                //voipPeer.Send(memoryManager.Memory.Slice((int)((byte*)packet - memoryManager.Pointer), packetLength));
            }
        }

        public static bool TryGetEncodedDataForSpeaker(out ArraySegment<byte> packet)
        {
            lock (packetBufferLock)
            {
                return packetBuffer.TryDequeue(out packet);
            }
        }

        private static void StartMicInput()
        {
            micInputChannel = Bass.BASS_RecordStart(SAMPLE_RATE, NUM_CHANNELS, BASSFlag.BASS_DEFAULT, MIC_BUFFER_MS, opusEncoder.RecordingProcess, nint.Zero);
            fxCompressorHandle = Bass.BASS_ChannelSetFX(micInputChannel, BASSFXType.BASS_FX_BFX_COMPRESSOR2, 0);
            Bass.BASS_FXSetParameters(fxCompressorHandle, compressorSettings);
        }

        private static void StartSpeakerOutput()
        {
            speakerOutputChannel = Bass.BASS_StreamCreate(SAMPLE_RATE, NUM_CHANNELS, BASSFlag.BASS_DEFAULT, opusDecoder.StreamProcess, 0);
            Bass.BASS_ChannelPlay(speakerOutputChannel, false);
        }

        public static void AddAudioSamples(ArraySegment<byte> data)
        {
            lock (packetBufferLock)
            {
                packetBuffer.Enqueue(data);
            }
        }

        internal static void SetVoipPeer(VoipPeer voipPeer)
        {
            AudioController.voipPeer = voipPeer;
        }
    }
}
