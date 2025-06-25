using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SoundRadar
{
    public partial class HotkeyManager : IDisposable
    {
        private Window window;
        private int hotkeyId;
        private Action onHotkey;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public HotkeyManager(Window window, uint modifiers, uint key, Action onHotkey)
        {
            this.window = window;
            this.onHotkey = onHotkey;
            hotkeyId = GetHashCode();

            var helper = new WindowInteropHelper(window);
            RegisterHotKey(helper.Handle, hotkeyId, modifiers, key);

            ComponentDispatcher.ThreadPreprocessMessage += ThreadPreprocessMessageMethod;
        }

        private void ThreadPreprocessMessageMethod(ref MSG msg, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg.message == WM_HOTKEY && (int)msg.wParam == hotkeyId)
            {
                onHotkey?.Invoke();
                handled = true;
            }
        }

        public void Dispose()
        {
            var helper = new WindowInteropHelper(window);
            UnregisterHotKey(helper.Handle, hotkeyId);
            ComponentDispatcher.ThreadPreprocessMessage -= ThreadPreprocessMessageMethod;
        }
    }
}