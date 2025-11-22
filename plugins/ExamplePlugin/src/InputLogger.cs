using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace InputLogging
{
    delegate void KeyEventHandler(object sender, NativeWindowMessage message, Keys keys, int scanCode, int time);

    delegate void MouseEventHandler(object sender, NativeWindowMessage message, Point location, short data, int time);

    enum NativeWindowMessage
    {
        WM_KEYDOWN    = 0x0100,
        WM_KEYUP      = 0x0101,
        WM_SYSKEYDOWN = 0x0104,

        WM_LBUTTONDOWN = 0x0201,
        WM_LBUTTONUP   = 0x0202,
        WM_MOUSEMOVE   = 0x0200,
        WM_MOUSEWHEEL  = 0x020A,
        WM_RBUTTONDOWN = 0x0204,
        WM_RBUTTONUP   = 0x0205
    }

    sealed class InputLogger : IDisposable
    {
        enum HookType
        {
            WH_KEYBOARD_LL = 13,
            WH_MOUSE_LL    = 14
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public int dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public int   mouseData;
            public int   flags;
            public int   time;
            public int   dwExtraInfo;
        }

        delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

        static IntPtr g_keyboardHook, g_mouseHook;

        static readonly List<InputLogger> g_instances = new List<InputLogger>();

        static InputLogger()
        {
            g_keyboardHook = SetWindowsHookEx(HookType.WH_KEYBOARD_LL, KeyboardHookCallback);
            g_mouseHook    = SetWindowsHookEx(HookType.WH_MOUSE_LL, MouseHookCallback);
        }

        public InputLogger()
        {
            g_instances.Add(this);
        }

        ~InputLogger()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing)
        {
            if (disposing)
            {
                g_instances.Remove(this);
            }

            if (g_instances.Count == 0)
            {
                UnhookWindowsHookEx(g_keyboardHook);
                UnhookWindowsHookEx(g_mouseHook);
            }
        }

        public event KeyEventHandler KeyEvent;
        
        public event MouseEventHandler MouseEvent;

        static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                try
                {
                    var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    var message    = (NativeWindowMessage)wParam;
                    var location   = new Point(hookStruct.pt.x, hookStruct.pt.y);

                    foreach (var instance in g_instances)
                    {
                        instance.MouseEvent?.Invoke(instance, message, location, HIWORD(hookStruct.mouseData), hookStruct.time);
                    }
                }
                catch (Exception) { }
            }

            return CallNextHookEx(g_mouseHook, nCode, wParam, lParam);
        }

        public static short HIWORD(int a)
        {
            return ((short)(a >> 16));
        }

        static IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                try
                {
                    var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    var message    = (NativeWindowMessage)wParam;

                    foreach (var instance in g_instances)
                    {
                        instance.KeyEvent?.Invoke(instance, message, (Keys)hookStruct.vkCode, hookStruct.scanCode, hookStruct.time);
                    }
                }
                catch (Exception) { }
            }

            return CallNextHookEx(g_keyboardHook, nCode, wParam, lParam);
        }

        static IntPtr SetWindowsHookEx(HookType idHook, LowLevelHookProc proc)
        {
            using (Process      currentProcess = Process.GetCurrentProcess())
            using (ProcessModule currentModule = currentProcess.MainModule)
            {
                return SetWindowsHookEx((int)idHook, proc, GetModuleHandle(currentModule.ModuleName), 0);
            }
        }

        [DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    }
}
