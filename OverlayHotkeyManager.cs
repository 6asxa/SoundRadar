using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SoundRadar
{
    public class OverlayHotkeyManager : IDisposable
    {
        private readonly Form form;
        private readonly int hotkeyId = 0x1234;
        private readonly Action onHotkey;
        private bool disposed = false;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public OverlayHotkeyManager(Form form, uint modifiers, uint key, Action onHotkey)
        {
            this.form = form;
            this.onHotkey = onHotkey;
            RegisterHotKey(form.Handle, hotkeyId, modifiers, key);
            form.HandleCreated += (s, e) => RegisterHotKey(form.Handle, hotkeyId, modifiers, key);
            form.HandleDestroyed += (s, e) => UnregisterHotKey(form.Handle, hotkeyId);

            form.KeyPreview = true;
            form.KeyDown += Form_KeyDown;
        }

        private void Form_KeyDown(object? sender, KeyEventArgs e)
        {
            // Не нужен, если используете RegisterHotKey
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                UnregisterHotKey(form.Handle, hotkeyId);
                disposed = true;
            }
        }
    }
}