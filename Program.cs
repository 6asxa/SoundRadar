using System;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using NAudio.CoreAudioApi;

namespace SoundRadar
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

            var wpfApp = new System.Windows.Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };

            string selectedDeviceName = null;

            var selector = new DeviceSelectorWindow();
            if (selector.ShowDialog() == true)
            {
                selectedDeviceName = selector.SelectedDevice;
            }
            else
            {
                wpfApp.Shutdown();
                return;
            }

            GameOverlayWindow overlay = null;
            TrayIconManager trayManager = null;

            try
            {
                // === Получаем MMDevice ===
                var enumerator = new MMDeviceEnumerator();

                var device = enumerator
                    .EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                    .FirstOrDefault(d => d.FriendlyName == selectedDeviceName)
                    ?? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);

                if (device == null)
                    throw new Exception("Audio device not found.");

                // === Создаём overlay ===
                overlay = new GameOverlayWindow(device);
                overlay.Create();

                trayManager = new TrayIconManager(overlay, wpfApp);

                System.Windows.Forms.Application.Run();

            }
            finally
            {
                trayManager?.Dispose();
                overlay?.Dispose();
                wpfApp.Shutdown();
            }
        }
    }
}
