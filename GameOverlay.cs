using GameOverlay.Drawing;
using GameOverlay.Windows;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SoundRadar
{
    public class GameOverlayWindow : StickyWindow
    {
        private GameOverlay.Drawing.Rectangle _squareRect;
        private readonly int _squareSize = 15;
        private readonly AudioProcessor _audioProcessor;
        private GameOverlay.Drawing.SolidBrush _redBrush;
        private GameOverlay.Drawing.SolidBrush _translucentBlackBrush;
        private GameOverlay.Drawing.Graphics _graphics;
        private GameOverlay.Drawing.Font _font;
        private GameOverlay.Drawing.SolidBrush _fontBrush;
        private bool _graphicsInitialized;

        private GlobalHotkeyReceiver _hotkeyReceiver;

        public GameOverlayWindow(string deviceName, int channelCount)
            : base()
        {
            // Настройки окна
            this.Width = 125;
            this.Height = 125;
            var screenWidth = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width;
            var screenHeight = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height;
            this.X = (screenWidth - this.Width) / 2;
            this.Y = (screenHeight - this.Height) / 2 + 300;
            this.IsTopmost = true;
            this.IsVisible = true;
            this.FPS = 60;

            this.SetupGraphics += OnSetupGraphics;
            this.DestroyGraphics += OnDestroyGraphics;
            this.DrawGraphics += OnDrawGraphics;

            UpdateSquarePosition(this.Width / 2, this.Height / 2);

            _audioProcessor = new AudioProcessor(deviceName, channelCount, OnVolumes);
            _audioProcessor.Start();

            Debug.WriteLine("GameOverlayWindow initialized");

            // Регистрируем глобальный хоткей Ctrl+T
            _hotkeyReceiver = new GlobalHotkeyReceiver(0x0002, 0x54, () =>
            {
                Debug.WriteLine("Горячая клавиша Ctrl+T нажата");
                BringWindowToFront();
            });
        }

        private void BringWindowToFront()
        {
            if (!this.IsVisible)
                this.Show();

            this.IsTopmost = false;
            this.IsTopmost = true;

            Debug.WriteLine("Окно поставлено поверх экрана");
        }

        private void OnSetupGraphics(object sender, SetupGraphicsEventArgs e)
        {
            Debug.WriteLine("Setting up graphics");
            _graphics = e.Graphics;

            if (_graphics == null)
            {
                Debug.WriteLine("Graphics is null");
                return;
            }

            try
            {
                _redBrush = _graphics.CreateSolidBrush(255, 0, 0);
                _translucentBlackBrush = _graphics.CreateSolidBrush(0, 0, 0, 120);
                _graphicsInitialized = _redBrush != null && _translucentBlackBrush != null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing graphics: {ex.Message}");
                _graphicsInitialized = false;
            }
        }

        private void OnDestroyGraphics(object sender, DestroyGraphicsEventArgs e)
        {
            Debug.WriteLine("Destroying graphics");
            _graphicsInitialized = false;
            _redBrush?.Dispose();
            _translucentBlackBrush?.Dispose();
            _redBrush = null;
            _translucentBlackBrush = null;
        }

        private void UpdateSquarePosition(int x, int y)
        {
            _squareRect = new GameOverlay.Drawing.Rectangle(
                x - _squareSize / 2,
                y - _squareSize / 2,
                x + _squareSize / 2,
                y + _squareSize / 2
            );

            Debug.WriteLine($"Square position updated to X: {x}, Y: {y}");
        }

        private void OnVolumes(float[] volumes)
        {
            if (!_graphicsInitialized || volumes == null || volumes.Length < 2)
            {
                Debug.WriteLine("Invalid or insufficient audio data");
                return;
            }

            int centerX = Width / 2;
            int centerY = Height / 2;

            if (volumes.Length >= 2)
            {
                float sum = volumes.Sum();
                double balanceX = sum > 0.0001f ? (volumes[1] - volumes[0]) / sum : 0; // Левый-правый баланс
                centerX = (int)(Width / 2 + (balanceX * (Width / 2 - _squareSize)));
                centerX = Math.Clamp(centerX, _squareSize / 2, Width - _squareSize / 2);

                if (volumes.Length >= 4) // Например, для 5.1 или 7.1
                {
                    double balanceY = sum > 0.0001f ? (volumes[2] - volumes[3]) / sum : 0; // Фронт-тыл
                    centerY = (int)(Height / 2 + (balanceY * (Height / 2 - _squareSize)));
                    centerY = Math.Clamp(centerY, _squareSize / 2, Height - _squareSize / 2);
                }

                Debug.WriteLine($"Audio volumes: {string.Join(", ", volumes)}, BalanceX: {balanceX}, New X: {centerX}, New Y: {centerY}");
            }

            UpdateSquarePosition(centerX, centerY);
        }

        private void OnDrawGraphics(object sender, DrawGraphicsEventArgs e)
        {
            if (!_graphicsInitialized || _redBrush == null || _translucentBlackBrush == null)
                return;

            try
            {
                var gfx = e.Graphics;
                gfx.ClearScene();
                gfx.FillRectangle(_translucentBlackBrush, 0, 0, Width, Height);
                gfx.FillRectangle(_redBrush, _squareRect);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"DrawGraphics error: {ex.Message}");
            }
        }

        public new void Dispose()
        {
            Debug.WriteLine("Disposing GameOverlayWindow");

            this.SetupGraphics -= OnSetupGraphics;
            this.DestroyGraphics -= OnDestroyGraphics;
            this.DrawGraphics -= OnDrawGraphics;

            _audioProcessor?.Dispose();
            _hotkeyReceiver?.Dispose();

            _redBrush?.Dispose();
            _translucentBlackBrush?.Dispose();
            _font?.Dispose();
            _fontBrush?.Dispose();

            base.Dispose();
        }

        // Встроенный класс для регистрации хоткея без зависимостей от WPF/WinForms
        private class GlobalHotkeyReceiver : NativeWindow, IDisposable
        {
            private const int WM_HOTKEY = 0x0312;
            private readonly int hotkeyId;
            private readonly uint modifiers;
            private readonly uint key;
            private readonly Action onHotkey;

            [DllImport("user32.dll")]
            private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
            [DllImport("user32.dll")]
            private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

            public GlobalHotkeyReceiver(uint modifiers, uint key, Action onHotkey)
            {
                this.modifiers = modifiers;
                this.key = key;
                this.onHotkey = onHotkey;
                this.hotkeyId = GetHashCode();

                CreateHandle(new CreateParams());
                RegisterHotKey(this.Handle, hotkeyId, modifiers, key);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY && (int)m.WParam == hotkeyId)
                {
                    onHotkey?.Invoke();
                }

                base.WndProc(ref m);
            }

            public void Dispose()
            {
                UnregisterHotKey(this.Handle, hotkeyId);
                DestroyHandle();
            }
        }
    }
}
