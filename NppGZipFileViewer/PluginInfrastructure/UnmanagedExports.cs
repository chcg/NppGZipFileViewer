// NPP plugin platform for .Net v0.94.00 by Kasper B. Graversen etc.
using Kbg.NppPluginNET.PluginInfrastructure;
using NppPlugin.DllExport;
using System;
using System.Runtime.InteropServices;

namespace Kbg.NppPluginNET;

internal class UnmanagedExports
{
    [DllExport(CallingConvention = CallingConvention.Cdecl)]
    private static bool isUnicode() => true;

    [DllExport(CallingConvention = CallingConvention.Cdecl)]
    private static void setInfo(NppData notepadPlusData)
    {
        PluginBase.nppData = notepadPlusData;
        Main.CommandMenuInit();
    }

    [DllExport(CallingConvention = CallingConvention.Cdecl)]
    private static IntPtr getFuncsArray(ref int nbF)
    {
        nbF = PluginBase._funcItems.Items.Count;
        return PluginBase._funcItems.NativePointer;
    }

    [DllExport(CallingConvention = CallingConvention.Cdecl)]
    private static uint messageProc(uint Message, IntPtr wParam, IntPtr lParam) => 1;

    private static IntPtr _ptrPluginName = IntPtr.Zero;
    [DllExport(CallingConvention = CallingConvention.Cdecl)]
    private static IntPtr getName()
    {
        if (_ptrPluginName == IntPtr.Zero)
            _ptrPluginName = Marshal.StringToHGlobalUni(Main.PluginName);
        return _ptrPluginName;
    }

    [DllExport(CallingConvention = CallingConvention.Cdecl)]
    private static void beNotified(IntPtr notifyCode)
    {
        ScNotification notification = (ScNotification)Marshal.PtrToStructure(notifyCode, typeof(ScNotification));
        if (notification.Header.Code == (uint)NppMsg.NPPN_TBMODIFICATION)
        {
            PluginBase._funcItems.RefreshItems();
            Main.SetToolBarIcon();
        }
        else if (notification.Header.Code == (uint)NppMsg.NPPN_SHUTDOWN)
        {
            Main.PluginCleanUp();
            Marshal.FreeHGlobal(_ptrPluginName);
        }
        else
        {
            Main.OnNotification(notification);
        }
    }
}
