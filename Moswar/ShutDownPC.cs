using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace Moswar
{
    public class clsExitWindows
    {

        [StructLayout(LayoutKind.Sequential)]
        public struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }
        
        [StructLayout(LayoutKind.Sequential)]
        public struct TOKEN_PRIVILEGES
        {
            public UInt32 PrivilegeCount;
            public LUID Luid;
            public UInt32 Attributes;
        }

        [DllImport("kernel32.dll", ExactSpelling = true)]
        internal static extern IntPtr GetCurrentProcess();

        [DllImport("Advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccesss, out IntPtr tokenHandle);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern Boolean CloseHandle(IntPtr hObject);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool LookupPrivilegeValue(string lpsystemname, string lpname, [MarshalAs(UnmanagedType.Struct)] ref LUID lpLuid);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AdjustTokenPrivileges(IntPtr tokenHandle, [MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges, [MarshalAs(UnmanagedType.Struct)]ref TOKEN_PRIVILEGES newstate, uint bufferlength, IntPtr previousState, IntPtr returnlength);
        
        [DllImport("user32")]
        internal static extern bool ExitWindowsEx(uint uFlags, uint dwReason);

        internal const int SE_PRIVILEGE_ENABLED = 0x00000002;
        internal const int TOKEN_QUERY = 0x00000008;
        internal const int TOKEN_ADJUST_PRIVILEGES = 0x00000020;
        internal const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";
        internal const int EWX_LOGOFF = 0x00000000;
        internal const int EWX_SHUTDOWN = 0x00000001;
        internal const int EWX_REBOOT = 0x00000002;
        internal const int EWX_FORCE = 0x00000004;
        internal const int EWX_POWEROFF = 0x00000008;
        internal const int EWX_FORCEIFHUNG = 0x00000010;

        private static void ExitWindows(uint Param = EWX_POWEROFF | EWX_FORCE)
        {
            TOKEN_PRIVILEGES TP;
            IntPtr hProc = GetCurrentProcess();
            IntPtr hTok = IntPtr.Zero;
            
            TP.PrivilegeCount = 1;
            TP.Luid = new LUID();
            TP.Attributes = SE_PRIVILEGE_ENABLED;
            try
            {
                if (LookupPrivilegeValue(null, SE_SHUTDOWN_NAME, ref TP.Luid))
                {
                    if (OpenProcessToken(hProc, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out hTok))
                    {
                        if (AdjustTokenPrivileges(hTok, false, ref TP, 1024, IntPtr.Zero, IntPtr.Zero))
                        {
                            bool bRet = ExitWindowsEx(Param, 0);
                        }
                    }
                }
            }
            finally //Закрываем открытый до этого процесс повышения привилегий.
            {
                if (hTok != IntPtr.Zero) CloseHandle(hTok);
            }             
        }

        public static void ShutDown()
        {
            ExitWindows(EWX_POWEROFF | EWX_FORCE);
        }
        public static void Reboot()
        {
            ExitWindows(EWX_REBOOT | EWX_FORCE);
        }
    }        
}



