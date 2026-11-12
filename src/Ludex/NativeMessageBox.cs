using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ludex;

/// <summary>
/// Окно сообщения средствами Windows (MessageBoxW). Нужно для фатальных ошибок:
/// оно работает даже тогда, когда Avalonia не смогла запуститься, а консоли у WinExe нет.
/// </summary>
internal static class NativeMessageBox
{
    private const uint MB_OK = 0x00000000;
    private const uint MB_ICONERROR = 0x00000010;
    private const uint MB_TOPMOST = 0x00040000;
    private const uint MB_SETFOREGROUND = 0x00010000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "MessageBoxW")]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    public static bool Show(string message, string caption = "Ludex")
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }
        try
        {
            MessageBox(IntPtr.Zero, message, caption, MB_OK | MB_ICONERROR | MB_TOPMOST | MB_SETFOREGROUND);
            return true;
        }
        catch (Exception ex)
        {
            AppPaths.Log("NativeMessageBox: " + ex.Message);
            return false;
        }
    }
}
