using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Xml.Linq;
using FragLabs.Audio.Codecs;
using NobleConnect;
using Un4seen.Bass;
using Un4seen.Bass.AddOn.Enc;
using Un4seen.Bass.AddOn.EncOpus;
using Un4seen.Bass.AddOn.Opus;
using Un4seen.Bass.Misc;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Commons
{
    internal class AudioController
    {
        #region Input stuff

        // This does nothing but must exist for the encoder to work
        private RECORDPROC? dummyRecordingProcess;
        // The process that catches the encoded data from the mic to do something with it
        private ENCODEPROC? encoderProcess;
        // Magical black box that takes in a BASS channel and spits out the encoded bytes
        //private EncoderOPUS? opus = null;
        // The BASS channel that received MIC input
        private int micInputChannel = 0;

        // For controlling input volume
        int inputVolumeEffect;
        // For compression if we use it
        int compressorEffect;

        #endregion Input stuff

        #region Output stuff

        // Used for storing encoded bytes that must be received before the output stream can be created.
        // Creating a stream that can decode our encoded data requires some header bytes in order to set up the stream properly.
        // So we store those here before we create the stream. Once the stream is created we don't need this anymore
        // and the encoded data can be piped directly to the stream.
        private BASSBuffer? outputBuffer = new BASSBuffer(.5f, 44100, 1, 16); // 44.1kHz, 16-bit, mono (like we record!)
        object outputBufferLock = new object();

        STREAMPROC? streamProc;

        // The BASS channel that speakers play from
        private int speakerOutputChannel = 0;

        #endregion Output stuff

        public event Action<ArraySegment<byte>>? OnWaveDataIn;

        public enum Ctl
        {
            SetBitrateRequest = 4002,
            GetBitrateRequest = 4003,
            SetInbandFECRequest = 4012,
            GetInbandFECRequest = 4013
        }

        [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr opus_encoder_create(int Fs, int channels, int application, out IntPtr error);

        [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void opus_encoder_destroy(IntPtr encoder);

        [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_encode(IntPtr st, byte[] pcm, int frame_size, IntPtr data, int max_data_bytes);

        [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_encode(IntPtr st, IntPtr pcm, int frame_size, IntPtr data, int max_data_bytes);

        [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_encode_float(IntPtr st, float[] pcm, int frame_size, IntPtr data, out int out_data_bytes);

        [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr opus_decoder_create(int Fs, int channels, out IntPtr error);

        [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void opus_decoder_destroy(IntPtr decoder);

        [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_decode(IntPtr st, byte[] data, int len, IntPtr pcm, int frame_size, int decode_fec);

        [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_decode(IntPtr st, IntPtr data, int len, IntPtr pcm, int frame_size, int decode_fec);

        [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_encoder_ctl(IntPtr st, Ctl request, int value);

        [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_encoder_ctl(IntPtr st, Ctl request, out int value);

        internal AudioController()
        {
            InitializeBass();
            StartMicInput();
            StartPlaying();
        }

        private void InitializeBass()
        {
            var wih = new WindowInteropHelper(Application.Current.MainWindow);
            IntPtr hWnd = wih.Handle;
            if (Bass.BASS_Init(-1, 48000, BASSInit.BASS_DEVICE_DEFAULT, hWnd))
            {
                BASS_INFO info = new BASS_INFO();
                Bass.BASS_GetInfo(info);
                Trace.WriteLine(info.ToString());
            }
            else
            {
                Trace.WriteLine("Bass_Init error!");
            }

            LoadBassPlugin();
        }

        private void LoadBassPlugin()
        { 
            Bass.BASS_PluginLoad("bassenc.dll");
            Bass.BASS_PluginLoad("bassopus.dll");
            Bass.BASS_PluginLoad("bassenc_opus.dll");
            //pluginFX = Bass.BASS_PluginLoad("bass_fx.dll");
        }

        EncoderWAV bla;
        int opus;
        IntPtr opus_encoder;
        IntPtr opus_decoder;
        byte[] encodedData;
        int MaxEncodedBytes = 48000;
        unsafe void StartMicInput()
        {
            if (!Bass.BASS_RecordInit(-1))
            {
                Trace.WriteLine("Bass_Init error!");
            }

            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_REC_BUFFER, 5000);
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_NET_BUFFER, 0);
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_BUFFER, 0);
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_NET_PREBUF, 0);
            Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_NET_PREBUF_WAIT, false);

            opus_encoder = opus_encoder_create(48000, 1, 2048, out IntPtr error);

            encodedData = new byte[MaxEncodedBytes];
            byte[] pcmData = new byte[MaxEncodedBytes];
            int pcmDataWriteIndex = 0;
            int pcmDataReadIndex = 0;
            dummyRecordingProcess = new RECORDPROC((handle, buffer, length, user) => {
                int totalPCMBytesProcessed = 0;
                int totalEncodedBytes = 0;

                if (pcmDataWriteIndex + length > pcmData.Length)
                {
                    Array.Copy(pcmData, pcmDataReadIndex, pcmData, 0, (pcmDataWriteIndex - pcmDataReadIndex));
                    pcmDataWriteIndex -= pcmDataReadIndex;
                    pcmDataReadIndex = 0;
                }

                Marshal.Copy(buffer, pcmData, pcmDataWriteIndex, length);
                pcmDataWriteIndex += length;

                int totalBytesWritten = 0;
                lock (outputBufferLock)
                {
                    while (totalPCMBytesProcessed + 480 <= length)
                    {
                        fixed (byte* encoded = encodedData, pcm = pcmData)
                        {
                            ushort numEncodedBytes = (ushort)opus_encode(opus_encoder, new IntPtr(pcm + pcmDataReadIndex), 240, new IntPtr(encoded + totalEncodedBytes), length - totalEncodedBytes);
                            byte firstPart = (byte)numEncodedBytes;
                            outputBuffer.Write(new IntPtr(&firstPart), 1);
                            totalBytesWritten++;
                            byte secondPart = (byte)(numEncodedBytes >> 8);
                            outputBuffer.Write(new IntPtr(&secondPart), 1);
                            totalBytesWritten++;
                            outputBuffer.Write(new IntPtr(encoded + totalEncodedBytes), numEncodedBytes);
                            totalBytesWritten += numEncodedBytes;
                            totalEncodedBytes += numEncodedBytes;
                            totalPCMBytesProcessed += 480;
                            pcmDataReadIndex += 480;
                        }
                    }
                }
                return true;
            });

            micInputChannel = Bass.BASS_RecordStart(48000, 1, BASSFlag.BASS_STREAM_DECODE | BASSFlag.BASS_RECORD_PAUSE, 10, dummyRecordingProcess, IntPtr.Zero);

            Bass.BASS_ChannelPlay(micInputChannel, false);
        }

        unsafe void StartPlaying()
        { 
            new Thread(() => {
                opus_decoder = opus_decoder_create(48000, 1, out IntPtr error);

                byte[] encodedData = new byte[MaxEncodedBytes];
                int encodedDataReadIndex = 0;
                int encodedDataWriteIndex = 0;
                streamProc = new STREAMPROC((handle, buffer, numDesiredBytes, user) => {
                    int totalPCMBytesProcessed = 0;
                    int totalEncodedBytesProcessed = 0;
                    int numNewBytes = outputBuffer.Count(0);

                    if (numNewBytes != 0)
                    {
                        if (encodedDataWriteIndex + numNewBytes > MaxEncodedBytes)
                        {
                            // Copy unprocessed encoded data to the start of the array so we don't overrun
                            Array.Copy(encodedData, encodedDataReadIndex, encodedData, 0, (encodedDataWriteIndex - encodedDataReadIndex));
                            encodedDataWriteIndex -= encodedDataReadIndex;
                            encodedDataReadIndex = 0;
                        }

                        fixed (byte* encoded = encodedData)
                        {
                            lock (outputBufferLock)
                            {
                                numNewBytes = outputBuffer.Read(new IntPtr(encoded + encodedDataWriteIndex), numNewBytes, 0);
                            }
                        }
                    }
                    int numBytesAvailableToDecode = (encodedDataWriteIndex - encodedDataReadIndex) + numNewBytes;
                    while (totalPCMBytesProcessed < numDesiredBytes)
                    {
                        fixed (byte* encoded = encodedData)
                        {
                            byte* encodedIndex = encoded + encodedDataReadIndex + totalEncodedBytesProcessed;
                            ushort packetSize = 0;

                            // No room for size in remaining encoded data
                            if (numBytesAvailableToDecode - totalEncodedBytesProcessed < 2) break;
                            
                            packetSize = encodedIndex[0];
                            encodedIndex++;
                            packetSize += (ushort)(encodedIndex[0] << 8);
                            encodedIndex++;

                            // No room for the rest of the payload
                            if (numBytesAvailableToDecode - (totalEncodedBytesProcessed + sizeof(short)) < packetSize) break;

                            int numSamplesProcessed = opus_decode(opus_decoder, new IntPtr(encodedIndex), packetSize, new IntPtr(buffer + totalPCMBytesProcessed), 240, 0);
                            totalPCMBytesProcessed += numSamplesProcessed * sizeof(short); // This short is because pcm samples are 16 bit or something
                            totalEncodedBytesProcessed += packetSize + sizeof(short); // This short is because of the packet length
                        }
                    }
                    encodedDataReadIndex += totalEncodedBytesProcessed;
                    encodedDataWriteIndex += numNewBytes;
                    return totalPCMBytesProcessed;
                });

                speakerOutputChannel = Bass.BASS_StreamCreate(48000, 1, BASSFlag.BASS_DEFAULT, streamProc, 0);
                Bass.BASS_SetConfig(BASSConfig.BASS_CONFIG_NET_PREBUF, 0);
                Bass.BASS_ChannelSetAttribute(speakerOutputChannel, BASSAttribute.BASS_ATTRIB_NET_RESUME, 0);
                Bass.BASS_ChannelSetAttribute(speakerOutputChannel, BASSAttribute.BASS_ATTRIB_BUFFER, 0);

                Bass.BASS_ChannelPlay(speakerOutputChannel, false);
            }).Start();
        }

        internal void AddAudioSamples(byte[] audio, int numBytesRead)
        {
            //monBuffer.Write(audio, numBytesRead);
        }
    }
}
