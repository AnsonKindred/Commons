using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Un4seen.Bass;
using Un4seen.Bass.Misc;

namespace Commons
{
    internal class AudioController
    {

        public event Action<ArraySegment<byte>>? OnWaveDataIn;

        internal AudioController()
        {
            Encoder_Load();
        }

        private BASSBuffer monBuffer = new BASSBuffer(2f, 44100, 1, 16); // 44.1kHz, 16-bit, mono (like we record!)
        private RECORDPROC _myRecProc;
        //private ENCODEPROC _myEncProc;
        private int _recHandle = 0;
        private STREAMPROC? monProc = null;
        private int monStream = 0;
        int pluginSpx;
        int pluginOpus;
        int pluginFX;
        EncoderOPUS? opus = null;
        int volfx;
        int _compressor;

        private void Encoder_Load()
        {
            var wih = new WindowInteropHelper(Application.Current.MainWindow);
            IntPtr hWnd = wih.Handle;
            if (Bass.BASS_Init(-1, 44100, BASSInit.BASS_DEVICE_DEFAULT, hWnd))
            {
                BASS_INFO info = new BASS_INFO();
                Bass.BASS_GetInfo(info);
                Trace.WriteLine(info.ToString());
            }
            else
            {
                Trace.WriteLine("Bass_Init error!");
            }

            //pluginSpx = Bass.BASS_PluginLoad("bassopus.dll");
            //pluginOpus = Bass.BASS_PluginLoad("bassenc_opus.dll");
            //pluginFX = Bass.BASS_PluginLoad("bass_fx.dll");
            //pluginFX = Bass.BASS_PluginLoad("bass_ac3.dll");

            // init your recording device (we use the default device)
            if (!Bass.BASS_RecordInit(-1))
            {
                Trace.WriteLine("Bass_Init error!");
            }

            _myRecProc = new RECORDPROC(MyRecording);
            _recHandle = Bass.BASS_RecordStart(44100, 1, BASSFlag.BASS_DEFAULT, _myRecProc, IntPtr.Zero);
            //_compressor = Bass.BASS_ChannelSetFX(_recHandle, BASSFXType.BASS_FX_DX8_COMPRESSOR, 0);

            //BASS_DX8_COMPRESSOR compressorSettings = new BASS_DX8_COMPRESSOR(2, 15, 50, -24, 50, 0);
            //bool success = Bass.BASS_FXSetParameters(_compressor, compressorSettings);
            //if (!success)
            //{
            //    Trace.WriteLine(Bass.BASS_ErrorGetCode());
            //}
            //volfx = Bass.BASS_ChannelSetFX(_recHandle, BASSFXType.BASS_FX_VOLUME, 0);
            //BASS_FX_VOLUME_PARAM param = new BASS_FX_VOLUME_PARAM();
            //param.fTarget = 30;
            //Bass.BASS_FXSetParameters(volfx, param);
            //_recHandle = Bass.BASS_RecordStart(44100, 2, BASSFlag.BASS_RECORD_PAUSE, _myRecProc, IntPtr.Zero);

            //opus = new EncoderOPUS(_recHandle);
            //opus.IsStreaming = true;
            //opus.NoLimit = true;
            //_myEncProc = new ENCODEPROC(MyRecording);
            //opus.Start(_myEncProc, IntPtr.Zero, false);

            //Trace.WriteLine(opus.EncoderHandle);

            monProc = new STREAMPROC(MonitoringStream);
            monStream = Bass.BASS_StreamCreate(44100, 1, BASSFlag.BASS_DEFAULT, monProc, IntPtr.Zero);
            Bass.BASS_ChannelPlay(monStream, false);

            //EncodeLoop();
        }

        //private async void EncodeLoop()
        //{
        //    while (true)
        //    {
        //        float[] data = new float[1024];
        //        Bass.BASS_ChannelGetData(_recHandle, data, 1024);
        //        await Task.Delay(1);
        //    }
        //}

        private int MonitoringStream(int handle, IntPtr buffer, int length, IntPtr user)
        {
            return monBuffer.Read(buffer, length, user.ToInt32());
        }

        //private void MyRecording(int handle, int channel, IntPtr buffer, int length, IntPtr user)
        //{
        //    // user will contain our encoding handle
        //    if (length > 0 && buffer != IntPtr.Zero)
        //    {
        //        // if recording started...write the data to the encoder
        //        monBuffer.Write(buffer, length);
        //    }
        //}

        // the recording callback
        private unsafe bool MyRecording(int handle, IntPtr buffer, int length, IntPtr user)
        {
            // user will contain our encoding handle
            if (length > 0 && buffer != IntPtr.Zero)
            {
                byte[] someBuffer = new byte[length];
                Marshal.Copy(buffer, someBuffer, 0, length);
                OnWaveDataIn?.Invoke(someBuffer);
            }
            return true; // always continue recording
        }

        internal void AddAudioSamples(byte[] audio, int numBytesRead)
        {
            monBuffer.Write(audio, numBytesRead);
        }
    }
}
