using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class MouseSimulator
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    public enum MouseEventFlags : uint
    {
        LEFTDOWN = 0x0002,
        LEFTUP = 0x0004
    }

    public static void MouseEvent(MouseEventFlags value)
    {
        mouse_event((uint)value, 0, 0, 0, UIntPtr.Zero);
    }
}
