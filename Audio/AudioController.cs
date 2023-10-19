using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Fx;
using Un4seen.Bass.AddOn.Sfx;

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

        // Shared buffer because right now I'm just echoing the mic back out, eventually these will be separate buffers or go straight to a socket or something
        static byte[] backingBuffer = new byte[SAMPLE_RATE * NUM_CHANNELS * sizeof(short)];
        static int backingBufferWriteIndex;
        static Queue<ArraySegment<byte>> fakeUDPBuffer = new Queue<ArraySegment<byte>>();
        static object fakeUDPSharedBufferLock = new object();

        public static event Action<ArraySegment<byte>>? OnWaveDataIn;
        static int pluginFX;
        static int fxCompressorHandle;
        static BASS_BFX_COMPRESSOR2 compressorSettings;

        static AudioController()
        {
            opusEncoder = new OpusEncoder();
            opusDecoder = new OpusDecoder();
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

        public static void DoSomethingWithEncodedDataFromMic(nint packet, int packetLength)
        {
            lock (fakeUDPSharedBufferLock)
            {
                if (backingBufferWriteIndex + packetLength > backingBuffer.Length)
                {
                    backingBufferWriteIndex = 0;
                }
                Marshal.Copy(packet, backingBuffer, backingBufferWriteIndex, packetLength);
                fakeUDPBuffer.Enqueue(new ArraySegment<byte>(backingBuffer, backingBufferWriteIndex, packetLength));
                backingBufferWriteIndex += packetLength;
            }
        }

        public static bool TryGetEncodedDataForSpeaker(out ArraySegment<byte> packet)
        {
            lock (fakeUDPSharedBufferLock)
            {
                return fakeUDPBuffer.TryDequeue(out packet);
            }
        }

        private static void StartMicInput()
        {
            micInputChannel = Bass.BASS_RecordStart(SAMPLE_RATE, NUM_CHANNELS, BASSFlag.BASS_DEFAULT, 10, opusEncoder.RecordingProcess, nint.Zero);
            fxCompressorHandle = Bass.BASS_ChannelSetFX(micInputChannel, BASSFXType.BASS_FX_BFX_COMPRESSOR2, 0);
            Trace.WriteLine("bad fx: " + Bass.BASS_ErrorGetCode());
            compressorSettings = new BASS_BFX_COMPRESSOR2(-2.5f, -24, 2, .02f, 20, BASSFXChan.BASS_BFX_CHANALL);
            
            bool worked = Bass.BASS_FXSetParameters(fxCompressorHandle, compressorSettings);
            if (!worked)
            {
                Trace.WriteLine(Bass.BASS_ErrorGetCode());
            }
        }

        private static void StartSpeakerOutput()
        {
            speakerOutputChannel = Bass.BASS_StreamCreate(SAMPLE_RATE, NUM_CHANNELS, BASSFlag.BASS_DEFAULT, opusDecoder.StreamProcess, 0);
            Bass.BASS_ChannelPlay(speakerOutputChannel, false);
        }

        public static void AddAudioSamples(byte[] audio, int numBytesRead)
        {
            //monBuffer.Write(audio, numBytesRead);
        }
    }
}
