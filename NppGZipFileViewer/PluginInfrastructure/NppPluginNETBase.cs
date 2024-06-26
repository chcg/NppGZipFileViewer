﻿// NPP plugin platform for .Net v0.94.00 by Kasper B. Graversen etc.
using System;

namespace Kbg.NppPluginNET.PluginInfrastructure;

internal class PluginBase
{
    internal static NppData nppData;
    internal static FuncItems _funcItems = new();

    internal static void SetCommand(int index, string commandName, NppFuncItemDelegate functionPointer) => SetCommand(index, commandName, functionPointer, new ShortcutKey(), false);

    internal static void SetCommand(int index, string commandName, NppFuncItemDelegate functionPointer, ShortcutKey shortcut) => SetCommand(index, commandName, functionPointer, shortcut, false);

    internal static void SetCommand(int index, string commandName, NppFuncItemDelegate functionPointer, bool checkOnInit) => SetCommand(index, commandName, functionPointer, new ShortcutKey(), checkOnInit);

    internal static void SetCommand(int index, string commandName, NppFuncItemDelegate functionPointer, ShortcutKey shortcut, bool checkOnInit)
    {
        FuncItem funcItem = new()
        {
            _cmdID = index,
            _itemName = commandName
        };
        if (functionPointer != null)
            funcItem._pFunc = new NppFuncItemDelegate(functionPointer);
        if (shortcut._key != 0)
            funcItem._pShKey = shortcut;
        funcItem._init2Check = checkOnInit;
        _funcItems.Add(funcItem);
    }

    internal static IntPtr GetCurrentScintilla()
    {
        _ = Win32.SendMessage(nppData._nppHandle, (uint)NppMsg.NPPM_GETCURRENTSCINTILLA, 0, out int curScintilla);
        return (curScintilla == 0) ? nppData._scintillaMainHandle : nppData._scintillaSecondHandle;
    }

    private static readonly Func<IScintillaGateway> gatewayFactory = () => new ScintillaGateway(GetCurrentScintilla());

    public static Func<IScintillaGateway> GetGatewayFactory() => gatewayFactory;
}
