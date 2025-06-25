using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using GameOverlay.Windows;
using System.Reflection;

namespace SoundRadar
{
    public class TrayIconManager : IDisposable
    {
        private NotifyIcon notifyIcon;
        private readonly GameOverlayWindow overlay;
        private readonly System.Windows.Application wpfApp;

        public TrayIconManager(GameOverlayWindow overlay, System.Windows.Application wpfApp)
        {
            this.overlay = overlay;
            this.wpfApp = wpfApp;
            InitializeTrayIcon();
        }

        private void InitializeTrayIcon()
        {
            notifyIcon = new NotifyIcon
            {
                Text = "SoundRadar",
                Visible = true
            };

            // Загружаем иконку из встроенного ресурса
            var assembly = Assembly.GetExecutingAssembly();
            using var iconStream = assembly.GetManifestResourceStream("SoundRadar.soundradar.ico");

            if (iconStream != null)
            {
                notifyIcon.Icon = new Icon(iconStream);
            }
            else
            {
                System.Windows.Forms.MessageBox.Show("Иконка не найдена в ресурсах!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Показать/Скрыть", null, (s, e) => ToggleOverlay());
            contextMenu.Items.Add("Настройки", null, (s, e) => OnShowSettingsWindow());
            contextMenu.Items.Add("Выход", null, (s, e) => OnExitApp());

            notifyIcon.ContextMenuStrip = contextMenu;
            notifyIcon.DoubleClick += (s, e) => ToggleOverlay();
        }

        private void ToggleOverlay()
        {
            overlay.IsVisible = !overlay.IsVisible;
        }

        private void OnShowSettingsWindow()
        {
            if (wpfApp == null) return;

            try
            {
                wpfApp.Dispatcher.Invoke(() =>
                {
                    var selector = new DeviceSelectorWindow();
                    if (selector.ShowDialog() == true)
                    {
                        // Обновляем настройки, если нужно
                    }
                });
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Ошибка открытия настроек: {ex.Message}",
                    "Ошибка",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void OnExitApp()
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            overlay.Dispose();

            System.Windows.Forms.Application.Exit();
            wpfApp.Dispatcher.Invoke(() => wpfApp.Shutdown());
        }

        public void Dispose()
        {
            notifyIcon?.Dispose();
        }
    }
}