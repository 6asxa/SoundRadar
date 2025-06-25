using System;
using System.Windows;
using System.Windows.Forms;

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

            string selectedDevice = null;
            int selectedChannels = 2;

            var selector = new DeviceSelectorWindow();
            if (selector.ShowDialog() == true)
            {
                selectedDevice = selector.SelectedDevice;
                selectedChannels = selector.SelectedChannels;
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
                overlay = new GameOverlayWindow(selectedDevice, selectedChannels);
                overlay.Create();

                trayManager = new TrayIconManager(overlay, wpfApp);

                System.Windows.Forms.Application.Run();
            }
            finally
            {
                // Очищаем ресурсы при выходе
                trayManager?.Dispose();
                overlay?.Dispose();
                wpfApp.Shutdown();
            }
        }
    }
}