using GameOverlay.Windows;
using NAudio.CoreAudioApi;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using OverlayRectangle = GameOverlay.Drawing.Rectangle;
using OverlayBrush = GameOverlay.Drawing.SolidBrush;
using OverlayGraphics = GameOverlay.Drawing.Graphics;


namespace SoundRadar
{
    public class GameOverlayWindow : StickyWindow
    {
        private OverlayRectangle _squareRect;
        private readonly int _squareSize = 15;

        private AudioProcessor _audioProcessor;
        private OverlayBrush _redBrush;
        private OverlayBrush _translucentBlackBrush;
        private OverlayGraphics _graphics;
        private bool _graphicsInitialized;

        private GlobalHotkeyReceiver _hotkeyReceiver;

        // === Движение ===
        private float _currentAngle;
        private float _currentIntensity;

        private float _smoothX;
        private float _smoothY;

        private const float MovementSmoothing = 0.2f;
        private const float ReturnToCenterSpeed = 0.05f;

        public GameOverlayWindow(MMDevice selectedDevice)
            : base()
        {
            Width = 125;
            Height = 125;

            var screenWidth = Screen.PrimaryScreen.Bounds.Width;
            var screenHeight = Screen.PrimaryScreen.Bounds.Height;

            X = (screenWidth - Width) / 2;
            Y = (screenHeight - Height) / 2 + 300;

            IsTopmost = true;
            IsVisible = true;
            FPS = 60;

            SetupGraphics += OnSetupGraphics;
            DestroyGraphics += OnDestroyGraphics;
            DrawGraphics += OnDrawGraphics;

            // Центр по умолчанию
            _smoothX = Width / 2f;
            _smoothY = Height / 2f;

            UpdateSquarePosition((int)_smoothX, (int)_smoothY);

            // Новый AudioProcessor (angle + intensity)
            _audioProcessor = new AudioProcessor(selectedDevice, OnDirection);
            _audioProcessor.Start();

            // Глобальный хоткей Ctrl+T
            _hotkeyReceiver = new GlobalHotkeyReceiver(0x0002, 0x54, BringWindowToFront);

            Debug.WriteLine("GameOverlayWindow initialized");
        }

        private void BringWindowToFront()
        {
            if (!IsVisible)
                Show();

            IsTopmost = false;
            IsTopmost = true;

            Debug.WriteLine("Overlay moved to front");
        }

        // ================= AUDIO CALLBACK =================

        private void OnDirection(float angleDeg, float intensity)
        {
            _currentAngle = angleDeg;
            _currentIntensity = Math.Clamp(intensity, 0f, 1f);
        }

        // ================= GRAPHICS =================

        private void OnSetupGraphics(object sender, SetupGraphicsEventArgs e)
        {
            _graphics = e.Graphics;

            if (_graphics == null)
                return;

            _redBrush = _graphics.CreateSolidBrush(255, 0, 0);
            _translucentBlackBrush = _graphics.CreateSolidBrush(0, 0, 0, 120);

            _graphicsInitialized = true;
        }

        private void OnDestroyGraphics(object sender, DestroyGraphicsEventArgs e)
        {
            _graphicsInitialized = false;

            _redBrush?.Dispose();
            _translucentBlackBrush?.Dispose();

            _redBrush = null;
            _translucentBlackBrush = null;
        }

        private void OnDrawGraphics(object sender, DrawGraphicsEventArgs e)
        {
            if (!_graphicsInitialized)
                return;

            var gfx = e.Graphics;

            try
            {
                gfx.ClearScene();
                gfx.FillRectangle(_translucentBlackBrush, 0, 0, Width, Height);

                UpdateSquareMovement();

                gfx.FillRectangle(_redBrush, _squareRect);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Draw error: {ex.Message}");
            }
        }

        // ================= ДВИЖЕНИЕ =================

        private void UpdateSquareMovement()
        {
            float centerX = Width / 2f;
            float centerY = Height / 2f;

            float maxRadius = (Width / 2f) - _squareSize;

            if (_currentIntensity > 0.01f)
            {
                float radius = maxRadius * _currentIntensity;
                float angleRad = _currentAngle * (float)Math.PI / 180f;

                float targetX = centerX + MathF.Sin(angleRad) * radius;
                float targetY = centerY - MathF.Cos(angleRad) * radius;

                _smoothX += (targetX - _smoothX) * MovementSmoothing;
                _smoothY += (targetY - _smoothY) * MovementSmoothing;
            }
            else
            {
                // Возврат в центр при тишине
                _smoothX += (centerX - _smoothX) * ReturnToCenterSpeed;
                _smoothY += (centerY - _smoothY) * ReturnToCenterSpeed;
            }

            int finalX = (int)Math.Clamp(_smoothX, _squareSize / 2, Width - _squareSize / 2);
            int finalY = (int)Math.Clamp(_smoothY, _squareSize / 2, Height - _squareSize / 2);

            UpdateSquarePosition(finalX, finalY);
        }

        private void UpdateSquarePosition(int x, int y)
        {
            _squareRect = new OverlayRectangle(
                x - _squareSize / 2,
                y - _squareSize / 2,
                x + _squareSize / 2,
                y + _squareSize / 2
            );
        }

        // ================= DISPOSE =================

        public new void Dispose()
        {
            SetupGraphics -= OnSetupGraphics;
            DestroyGraphics -= OnDestroyGraphics;
            DrawGraphics -= OnDrawGraphics;

            _audioProcessor?.Dispose();
            _hotkeyReceiver?.Dispose();

            _redBrush?.Dispose();
            _translucentBlackBrush?.Dispose();

            base.Dispose();
        }

        // ================= HOTKEY =================

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
                hotkeyId = GetHashCode();

                CreateHandle(new CreateParams());
                RegisterHotKey(Handle, hotkeyId, modifiers, key);
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
                UnregisterHotKey(Handle, hotkeyId);
                DestroyHandle();
            }
        }
    }
}
