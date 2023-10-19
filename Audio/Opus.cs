using System.Runtime.InteropServices;
using System;

namespace Commons.Audio
{
    internal static class Opus
    {
        public enum Application
        {
            // Best for most VoIP/videoconference applications where listening quality and intelligibility matter most
            OPUS_APPLICATION_VOIP = 2048,
            // Best for broadcast/high-fidelity application where the decoded audio should be as close as possible to the input
            OPUS_APPLICATION_AUDIO = 2049,
            // Only use when lowest-achievable latency is what matters most. Voice-optimized modes cannot be used.
            OPUS_APPLICATION_RESTRICTED_LOWDELAY = 2051
        }

        public enum Control
        {
            SetBitrateRequest = 4002,
            GetBitrateRequest = 4003,
            SetInbandFECRequest = 4012,
            GetInbandFECRequest = 4013
        }

        [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern nint opus_encoder_create(int Fs, int channels, int application, out nint error);

        [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void opus_encoder_destroy(nint encoder);

        [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_encode(nint st, byte[] pcm, int frame_size, nint data, int max_data_bytes);

        [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_encode(nint st, nint pcm, int frame_size, nint data, int max_data_bytes);

        [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_encode_float(nint st, float[] pcm, int frame_size, nint data, out int out_data_bytes);

        [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern nint opus_decoder_create(int Fs, int channels, out nint error);

        [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void opus_decoder_destroy(nint decoder);

        [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_decode(nint st, byte[] data, int len, nint pcm, int frame_size, int decode_fec);

        [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_decode(nint st, nint data, int len, nint pcm, int frame_size, int decode_fec);

        [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_encoder_ctl(nint st, Control request, int value);

        [DllImport("opus.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int opus_encoder_ctl(nint st, Control request, out int value);
    }
}
