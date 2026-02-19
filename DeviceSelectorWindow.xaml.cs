using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using NAudio.CoreAudioApi;
using System.Text.Json;
using System.IO;
using System.Diagnostics;
using System.Linq;

namespace SoundRadar
{
    public partial class DeviceSelectorWindow : Window
    {
        public string SelectedDevice { get; private set; }
        public int SelectedChannels { get; private set; }

        public DeviceSelectorWindow()
        {
            InitializeComponent();
            LoadDevices();
            ApplySettings();
            channelComboBox.SelectedIndex = 0; // По умолчанию стерео
        }

        private void LoadDevices()
        {
            try
            {
                var enumerator = new MMDeviceEnumerator();
                // <-- меняем Capture на Render
                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

                if (!devices.Any())
                {
                    System.Windows.MessageBox.Show((string)FindResource("NoDevicesError"), "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    DialogResult = false;
                    Close();
                    return;
                }

                var deviceNames = devices.Select(d => d.FriendlyName).ToList();
                deviceComboBox.ItemsSource = deviceNames;
                deviceComboBox.SelectedIndex = deviceNames.Any() ? 0 : -1;
                Trace.WriteLine($"{DateTime.Now}: Loaded {deviceNames.Count} audio devices");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{DateTime.Now}: Error loading devices: {ex.Message}");
                System.Windows.MessageBox.Show($"Failed to load audio devices: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                DialogResult = false;
                Close();
            }
        }


        private void DeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (deviceComboBox.SelectedItem != null)
            {
                SelectedDevice = deviceComboBox.SelectedItem.ToString();
                Trace.WriteLine($"{DateTime.Now}: Selected device: {SelectedDevice}");
            }
        }

        private void ChannelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (channelComboBox.SelectedItem != null)
            {
                var selectedItem = (channelComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
                SelectedChannels = selectedItem switch
                {
                    "2 (Stereo)" => 2,
                    "6 (5.1)" => 6,
                    "8 (7.1)" => 8,
                    _ => 2
                };
                Trace.WriteLine($"{DateTime.Now}: Selected channels: {SelectedChannels}");
            }
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            if (deviceComboBox.SelectedItem == null)
            {
                System.Windows.MessageBox.Show((string)FindResource("SelectDeviceError"), "Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedDevice = deviceComboBox.SelectedItem.ToString();
            SelectedChannels = channelComboBox.SelectedIndex switch
            {
                0 => 2,
                1 => 6,
                2 => 8,
                _ => 2
            };

            // Проверка поддержки каналов
            try
            {
                var enumerator = new MMDeviceEnumerator();
                var device = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                    .FirstOrDefault(d => d.FriendlyName == SelectedDevice);

                if (device != null && device.AudioClient.MixFormat.Channels < SelectedChannels)
                {
                    System.Windows.MessageBox.Show(string.Format((string)FindResource("ChannelError"),
                        device.AudioClient.MixFormat.Channels, SelectedChannels),
                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{DateTime.Now}: Error validating channels: {ex.Message}");
                System.Windows.MessageBox.Show($"Failed to validate device channels: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SaveSettings(SelectedDevice, SelectedChannels);
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private const string SettingsFile = "settings.json";

        private void SaveSettings(string deviceName, int channels)
        {
            try
            {
                var settings = new AppSettings { DeviceName = deviceName, Channels = channels };
                File.WriteAllText(SettingsFile, JsonSerializer.Serialize(settings));
                Trace.WriteLine($"{DateTime.Now}: Settings saved: Device={deviceName}, Channels={channels}");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{DateTime.Now}: Error saving settings: {ex.Message}");
                System.Windows.MessageBox.Show($"Failed to save settings: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private AppSettings? LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFile)) return null;
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile));
                Trace.WriteLine($"{DateTime.Now}: Settings loaded: Device={settings?.DeviceName}, Channels={settings?.Channels}");
                return settings;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"{DateTime.Now}: Error loading settings: {ex.Message}");
                return null;
            }
        }

        private void ApplySettings()
        {
            var settings = LoadSettings();
            if (settings != null && deviceComboBox.Items.Contains(settings.DeviceName))
            {
                deviceComboBox.SelectedItem = settings.DeviceName;
                channelComboBox.SelectedIndex = settings.Channels switch { 2 => 0, 6 => 1, 8 => 2, _ => 0 };
                SelectedDevice = settings.DeviceName;
                SelectedChannels = settings.Channels;
            }
        }

        public class AppSettings
        {
            public string? DeviceName { get; set; }
            public int Channels { get; set; }
        }
    }
}