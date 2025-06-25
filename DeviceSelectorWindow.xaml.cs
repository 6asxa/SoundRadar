using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using NAudio.CoreAudioApi;
using System.Text.Json;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Windows.Shapes; // Убедитесь, что это пространство имен есть
using System.Windows.Media;

namespace SoundRadar
{
    public partial class DeviceSelectorWindow : Window
    {
        public string SelectedDevice { get; private set; }
        public int SelectedChannels { get; private set; }
        private System.Windows.Shapes.Rectangle square; // Явное указание типа
        private const int squareSize = 40;

        public DeviceSelectorWindow()
        {
            InitializeComponent();
            LoadDevices();
            ApplySettings();

        }

        private void LoadDevices()
        {
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);

            if (!devices.Any())
            {
                System.Windows.MessageBox.Show("Нет доступных аудиоустройств!", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            deviceComboBox.ItemsSource = devices.Select(d => d.FriendlyName).ToList();
            deviceComboBox.SelectedIndex = 0;
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (deviceComboBox.SelectedItem == null)
            {
                System.Windows.MessageBox.Show("Пожалуйста, выберите устройство", "Ошибка",
                              MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedDevice = deviceComboBox.SelectedItem as string;
            SelectedChannels = channelComboBox.SelectedIndex switch
            {
                0 => 2,
                1 => 6,
                2 => 8,
                _ => 2
            };

            // Сохраняем настройки
            SaveSettings(SelectedDevice, SelectedChannels);

            DialogResult = true;
            Close();
        }

        private const string SettingsFile = "settings.json";

        private void SaveSettings(string deviceName, int channels)
        {
            var settings = new AppSettings { DeviceName = deviceName, Channels = channels };
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(settings));
        }

        private AppSettings? LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFile)) return null;
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile));
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{DateTime.Now}: Ошибка загрузки настроек: {ex.Message}");
                return null;
            }
        }

        private void ApplySettings()
        {
            var settings = LoadSettings();
            if (settings != null)
            {
                deviceComboBox.SelectedItem = settings.DeviceName;
                channelComboBox.SelectedIndex = settings.Channels switch { 2 => 0, 6 => 1, 8 => 2, _ => 0 };
            }
        }

    public class AppSettings
    {
        public string? DeviceName { get; set; }
        public int Channels { get; set; }
    }
    }
}
