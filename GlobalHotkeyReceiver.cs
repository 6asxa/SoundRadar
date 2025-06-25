using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SoundRadar
{
    public class GlobalHotkeyReceiver : NativeWindow, IDisposable
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
