using System.Runtime.InteropServices;
using System;
using System.Windows;
using System.Windows.Interop;

namespace Commons
{
    public partial class App : Application
    {
        const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;

        // Dark magic used to get dark mode title bar on the window.
        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref bool attrValue, int attrSize);

        // Windows hook up to this to get the dark mode title bar. It can't be called in the Window constructor so must happen
        public static void SetWindowDarkMode(Window window)
        {
            bool value = true;
            DwmSetWindowAttribute(new WindowInteropHelper(window).Handle, 20, ref value, Marshal.SizeOf(value));
            DwmSetWindowAttribute(new WindowInteropHelper(window).Handle, DWMWA_TRANSITIONS_FORCEDISABLED, ref value, Marshal.SizeOf(value));
        }

        public readonly CommonsContext DB = new CommonsContext();

        internal App()
        {
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            Window mainWindow = new MainWindow();
            MainWindow.WindowState = WindowState.Minimized;
            mainWindow.Loaded += MainWindow_Loaded;
            mainWindow.Show();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ((Window)sender).WindowState = WindowState.Normal;
            bool value = false;
            DwmSetWindowAttribute(new WindowInteropHelper(((Window)sender)).Handle, DWMWA_TRANSITIONS_FORCEDISABLED, ref value, Marshal.SizeOf(value));
        }
    }
}
