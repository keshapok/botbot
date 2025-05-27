using System;
using System.Runtime.InteropServices;

public static class MouseSimulator
{
    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

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
