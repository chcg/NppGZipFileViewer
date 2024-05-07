using Kbg.NppPluginNET.PluginInfrastructure;
using NppGZipFileViewer;
using NppGZipFileViewer.Forms;
using NppGZipFileViewer.Settings;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Kbg.NppPluginNET;

internal class Main
{
    static Main() => AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

    private static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
    {
        string executingAssemblyName = Assembly.GetExecutingAssembly().GetName().Name + ".dll";
        string targetAssemblyName = new AssemblyName(args.Name).Name + ".dll";
        using Stream stream =  Assembly.GetExecutingAssembly().GetManifestResourceStream(executingAssemblyName + "." + targetAssemblyName);
        if (stream != null)
        {
            using MemoryStream ms = new();
            stream.CopyTo(ms);
            return Assembly.Load(ms.GetBuffer());
        }

        string p = AppDomain.CurrentDomain.BaseDirectory;
        string path = System.Reflection.Assembly.GetCallingAssembly().Location;
        string libPath = Path.Combine(Path.GetDirectoryName(path), targetAssemblyName);
        return File.Exists(libPath) ? System.Reflection.Assembly.LoadFile(libPath) : null;
    }

    internal const string PluginName = "NppGZipFileViewer";
    private static readonly FileTracker fileTracker = new();
    private static string iniFilePath = null;
    private static readonly Bitmap tbBmp = NppGZipFileViewer.Properties.Resources.gzip_filled16;
    private static readonly Dictionary<IntPtr, Position> cursorPosition = [];
    private static readonly Dictionary<IntPtr, CompressionSettings> compressionBeforeSave = [];

    public static Preferences Preferences { get; set; }

    private static NotepadPPGateway nppGateway;
    private static ScintillaGateway scintillaGateway;

    public static void OnNotification(ScNotification notification)
    {
        _ = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
        switch (notification.Header.Code)
        {
            case (uint)NppMsg.NPPN_FILEOPENED:
                OpenFile(notification);
                break;
            case (uint)NppMsg.NPPN_FILEBEFORESAVE:
                try
                {
                    StringBuilder path = nppGateway.GetFullPathFromBufferId(notification.Header.IdFrom);

                    CompressionSettings compr = GetFileCompression(notification.Header.IdFrom);

                    // store the current compression settings
                    if (compressionBeforeSave.ContainsKey(notification.Header.IdFrom))
                        compressionBeforeSave[notification.Header.IdFrom] = compr;
                    else
                        compressionBeforeSave.Add(notification.Header.IdFrom, compr);

                    if (cursorPosition.ContainsKey(notification.Header.IdFrom))
                        cursorPosition[notification.Header.IdFrom] = scintillaGateway.GetCurrentPos();
                    else
                        cursorPosition.Add(notification.Header.IdFrom, scintillaGateway.GetCurrentPos());

                    if (compr == null) return;

                    scintillaGateway.BeginUndoAction();


                    using MemoryStream contentStream = NppGZipFileViewerHelper.GetContentStream(notification, path);
                    Encoding fileEncoding = fileTracker.GetEncoding(notification.Header.IdFrom) ?? new UTF8Encoding(false);
                    using MemoryStream encodedContentStream = NppGZipFileViewerHelper.Encode(contentStream, fileEncoding,compr);
                    NppGZipFileViewerHelper.SetEncodedText(encodedContentStream);
                    NppEncoding currentNppEncoding = (NppEncoding)nppGateway.GetBufferEncoding(notification.Header.IdFrom);
                    scintillaGateway.EndUndoAction();
                }
                catch (Exception ex) { _ = MessageBox.Show(ex.Message, "Error at FileBeforeSave", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                break;
            case (uint)NppMsg.NPPN_FILESAVED:
            {
                StringBuilder path = nppGateway.GetFullPathFromBufferId(notification.Header.IdFrom);
                CompressionSettings targetCompression = ShouldBeCompressed(notification);
                CompressionSettings oldCompressed = compressionBeforeSave.ContainsKey(notification.Header.IdFrom) ? compressionBeforeSave[notification.Header.IdFrom] : null;

                if (oldCompressed != targetCompression)
                {
                    // save again, but update file tracker based on toCompressed
                    if (targetCompression != null)
                    {
                        Encoding encoding = fileTracker.GetEncoding(notification.Header.IdFrom) ?? NppGZipFileViewerHelper.ResetEncoding();
                        fileTracker.Include(notification.Header.IdFrom, path, encoding, targetCompression);
                    }
                    else
                    {
                        fileTracker.Exclude(notification.Header.IdFrom, path);
                        scintillaGateway.Undo(); //undo compression
                        scintillaGateway.GotoPos(cursorPosition[notification.Header.IdFrom]);
                        scintillaGateway.EmptyUndoBuffer();
                    }
                    nppGateway.SwitchToFile(path);
                    scintillaGateway.Undo();
                    scintillaGateway.GotoPos(cursorPosition[notification.Header.IdFrom]);
                    nppGateway.MakeCurrentBufferDirty();
                    nppGateway.SaveCurrentFile();
                    return;
                }

                if (oldCompressed != null) // if compressed we need to undo the changes
                {
                    scintillaGateway.Undo();
                    scintillaGateway.GotoPos(cursorPosition[notification.Header.IdFrom]);

                    scintillaGateway.EmptyUndoBuffer();
                    scintillaGateway.SetSavePoint();
                    scintillaGateway.SetSavePoint();

                }
            }
            break;
            case (uint)NppMsg.NPPN_FILECLOSED:
            {
                fileTracker.Remove(notification.Header.IdFrom);
                _ = cursorPosition.Remove(notification.Header.IdFrom);
                _ = compressionBeforeSave.Remove(notification.Header.IdFrom);
                break;
            }
            case (uint)NppMsg.NPPN_BUFFERACTIVATED:
                // TODO: update Status Bar and Command Check
                UpdateStatusbar(notification.Header.IdFrom);
                UpdateCommandChecked(notification.Header.IdFrom);
                break;

        }

    }

    private static CompressionSettings GetFileCompression(IntPtr idFrom)
    {
        StringBuilder path = nppGateway.GetFullPathFromBufferId(idFrom);


        if (fileTracker.IsExcluded(idFrom))
            return null; // file excluded

        if (Preferences.GetCompressionBySuffix(path) == null && !fileTracker.IsIncluded(idFrom))
            return null; // neither suffix nor included -> no compression, nothing to do

        // either suffix or included:
        CompressionSettings compr = fileTracker.GetCompressor(idFrom);
        compr ??= Preferences.GetCompressionBySuffix(nppGateway.GetFullPathFromBufferId(idFrom));

        return compr;
    }

    private static void OpenFile(ScNotification notification)
    {
        StringBuilder path = nppGateway.GetFullPathFromBufferId(notification.Header.IdFrom);
        CompressionSettings sourceCompression = Preferences.GetCompressionBySuffix(nppGateway.GetFullPathFromBufferId(notification.Header.IdFrom));
        using MemoryStream gzContentStream = NppGZipFileViewerHelper.GetContentStream(notification, path);

        if (sourceCompression != null)
        {
            if (gzContentStream.Length == 0) // Empty file:
            {
                Encoding encoding = NppGZipFileViewerHelper.ResetEncoding();
                fileTracker.Include(notification.Header.IdFrom, path, encoding, sourceCompression);
                return;
            }
            Encoding enc = TryDecompress(gzContentStream, sourceCompression);
            if (enc != null)
            { // was able to decompress
                fileTracker.Include(notification.Header.IdFrom, path, enc, sourceCompression);
                return;
            }
        }
        if (Preferences.DecompressAll && gzContentStream.Length > 0)
            foreach (CompressionSettings compression in Preferences.EnumerateCompressions())
            {
                _ = gzContentStream.Seek(0, SeekOrigin.Begin);
                Encoding enc = TryDecompress(gzContentStream, compression);
                if (enc != null)
                { // was able to decompress
                    fileTracker.Include(notification.Header.IdFrom, path, enc, compression);
                    return;
                }
            }

        // no compression found:
        if (sourceCompression != null) // could not compress file although it has a specifix suffix -> exclude this file
            fileTracker.Exclude(notification.Header.IdFrom, path);
    }

    private static void UpdateStatusbar(IntPtr from, bool resetStatusbar = false)
    {
        if (fileTracker.IsIncluded(from))
        {
            Encoding enc = fileTracker.GetEncoding(from);
            string str = $"{fileTracker.GetCompressor(from).CompressionAlgorithm}/{enc.WebName.ToUpper()}";
            if (enc.GetPreamble().Length > 0)
                str += " BOM";
            _ = nppGateway.SetStatusBar(NppMsg.STATUSBAR_UNICODE_TYPE, str);
        }
        else if (resetStatusbar)
        {
            Encoding enc = NppGZipFileViewerHelper.ToEncoding((NppEncoding)nppGateway.GetBufferEncoding(from));
            string str = $"{enc.WebName.ToUpper()}";
            if (enc.GetPreamble().Length > 0)
                str += " BOM";
            _ = nppGateway.SetStatusBar(NppMsg.STATUSBAR_UNICODE_TYPE, str);
        }
    }
    private static void UpdateCommandChecked(IntPtr from)
    {
        nppGateway.SetMenuItemCheck(0, fileTracker.IsIncluded(from));
        CompressionSettings compr = fileTracker.GetCompressor(from);
        foreach (CompressionSettings posCompr in Preferences.EnumerateCompressions())
            nppGateway.SetMenuItemCheck(posCompr.CompressionAlgorithm, compr?.CompressionAlgorithm == posCompr.CompressionAlgorithm);
    }

    private static CompressionSettings ShouldBeCompressed(ScNotification notification)
    {
        string newPath = nppGateway.GetFullPathFromBufferId(notification.Header.IdFrom).ToString();

        // no path change -> file tracked
        string oldPath = fileTracker.GetStoredPath(notification.Header.IdFrom);
        if (newPath == oldPath)
        {
            if (fileTracker.IsIncluded(notification.Header.IdFrom)) // is tracked, so compress
                return fileTracker.GetCompressor(notification.Header.IdFrom);
            if (fileTracker.IsExcluded(notification.Header.IdFrom)) // is excluded, so don't compress
                return null;
            return Preferences.GetCompressionBySuffix(newPath); // no manually set information, store iff gz-suffix (should be tracked then, but who knows) 
        }

        // path changed

        // compression based on suffix changed: return compression for new path
        if (Preferences.GetCompressionBySuffix(oldPath) != Preferences.GetCompressionBySuffix(newPath))
            return Preferences.GetCompressionBySuffix(newPath);

        // same suffix type:

        // from gz to gz or non gz to non gz, use tracker

        if (fileTracker.IsIncluded(notification.Header.IdFrom))
            return fileTracker.GetCompressor(notification.Header.IdFrom);

        if (fileTracker.IsExcluded(notification.Header.IdFrom))
            return null;

        // not tracked -> go by suffix, should always return false, since gz-files should always be tracked
        return Preferences.GetCompressionBySuffix(newPath);
    }

    private static Encoding TryDecompress(Stream contentStream, CompressionSettings compression)
    {
        try
        {
            using MemoryStream decodedContentStream = NppGZipFileViewerHelper.Decode(contentStream, compression);
            Encoding encoding = NppGZipFileViewerHelper.SetDecodedText(decodedContentStream);

            nppGateway.SendMenuEncoding(NppEncoding.UTF8);
            _ = Win32.SendMessage(PluginBase.GetCurrentScintilla(), SciMsg.SCI_GOTOPOS, 0, 0);
            _ = Win32.SendMessage(PluginBase.GetCurrentScintilla(), SciMsg.SCI_EMPTYUNDOBUFFER, 0, 0);
            _ = Win32.SendMessage(PluginBase.GetCurrentScintilla(), SciMsg.SCI_SETSAVEPOINT, 0, 0);

            return encoding;
        }
        catch
        {
            return null;

        }
    }
    internal static void CommandMenuInit()
    {
        nppGateway = new NotepadPPGateway();
        scintillaGateway = new ScintillaGateway(PluginBase.GetCurrentScintilla());

        StringBuilder sbIniFilePath = new(Win32.MAX_PATH);
        _ = Win32.SendMessage(PluginBase.nppData._nppHandle, (uint)NppMsg.NPPM_GETPLUGINSCONFIGDIR, Win32.MAX_PATH, sbIniFilePath);
        iniFilePath = sbIniFilePath.ToString();
        if (!Directory.Exists(iniFilePath)) _ = Directory.CreateDirectory(iniFilePath);
        iniFilePath = Path.Combine(iniFilePath, PluginName + ".config");

        try
        {
            Preferences = Preferences.Deserialize(iniFilePath);
            if (Preferences.ShowDepcrecatedWarning)
                _ = MessageBox.Show("This plugin is deprecated, please switch to CompressedFileViewer", "Deprecated");

        }
        catch
        {
            Preferences = Preferences.Default;
            _ = MessageBox.Show("This plugin is deprecated, please switch to CompressedFileViewer", "Deprecated");
        }


        PluginBase.SetCommand(0, "Toggle Compression", ToogleCompress, false);
        PluginBase.SetCommand(1, "---", null);
        PluginBase.SetCommand(2, "Compress", Compress, false);
        PluginBase.SetCommand(3, "Decompress", Decompress, false);
        PluginBase.SetCommand(4, "---", null);
        PluginBase.SetCommand(5, "Settings", OpenSettings);
        PluginBase.SetCommand(6, "About", OpenAbout);
        PluginBase.SetCommand(7, "Credits", OpenCredits);
        PluginBase.SetCommand(8, "---", null);
        PluginBase.SetCommand(9, "Deprecated", () => MessageBox.Show("This plugin is deprecated, please switch to CompressedFileViewer", "Deprecated"));
        PluginBase.SetCommand(10, "---", null);
        SetCompressionCommands(11);
        SetToolBarIcon();
    }

    private static void SetCompressionCommands(int startIndex)
    {
        foreach (CompressionSettings compr in Preferences.EnumerateCompressions())
        {
            PluginBase.SetCommand(startIndex++, compr.CompressionAlgorithm, () => SetCompression(compr.CompressionAlgorithm));
        }
    }




    private static void Decompress()
    {
        DecompressForm decompressForm = new();
        if (decompressForm.ShowDialog() == DialogResult.OK)
        {
            _ = nppGateway.GetCurrentBufferId();
            CompressionSettings compr = decompressForm.CompressionSettings;
            using MemoryStream contentStream = NppGZipFileViewerHelper.GetCurrentContentStream();
            using MemoryStream decodedContentStream = NppGZipFileViewerHelper.Decode(contentStream, compr);
            Encoding enc = NppGZipFileViewerHelper.SetDecodedText(decodedContentStream);
            NppEncoding nppEnc = NppGZipFileViewerHelper.ToNppEncoding(enc);
            nppGateway.SendMenuEncoding(nppEnc);
        }
    }

    private static void Compress()
    {
        CompressForm compressForm = new();
        if (compressForm.ShowDialog() == DialogResult.OK)
        {
            IntPtr bufferId = nppGateway.GetCurrentBufferId();
            CompressionSettings compr = compressForm.CompressionSettings;
            using MemoryStream contentStream = NppGZipFileViewerHelper.GetCurrentContentStream();
            Encoding enc = fileTracker.GetEncoding(bufferId) ?? NppGZipFileViewerHelper.ToEncoding((NppEncoding) nppGateway.GetBufferEncoding(bufferId));
            using MemoryStream encodedContentStream = NppGZipFileViewerHelper.Encode(contentStream, enc, compr);
            NppGZipFileViewerHelper.SetEncodedText(encodedContentStream);
            nppGateway.SendMenuEncoding(NppEncoding.UTF8); // Set MenuEncoding to match scintillas internal buffer encoding
            // if it's not UTF-8... who cares
        }
    }

    private static void OpenCredits()
    {
        Credits credits = new();
        _ = credits.ShowDialog();
    }

    private static void OpenAbout()
    {
        AboutNppGZip about = new();
        about.Show();
    }

    private static void OpenSettings()
    {
        SettingsDialog settingDialog = new()
        {
            Preferences = Preferences
        };
        if (settingDialog.ShowDialog() == DialogResult.OK)
        {
            Preferences = settingDialog.Preferences;
            Preferences.Serialize(iniFilePath);

        }
    }

    internal static void SetToolBarIcon()
    {
        toolbarIcons tbIcons = new()
        {
            hToolbarBmp = tbBmp.GetHbitmap(),
            hToolbarIcon = tbBmp.GetHicon(),
            hToolbarIconDarkMode = tbBmp.GetHicon()
        };
        IntPtr pTbIcons = Marshal.AllocHGlobal(Marshal.SizeOf(tbIcons));
        Marshal.StructureToPtr(tbIcons, pTbIcons, false);
        _ = Win32.SendMessage(PluginBase.nppData._nppHandle, (uint)NppMsg.NPPM_ADDTOOLBARICON_FORDARKMODE, PluginBase._funcItems.Items[0]._cmdID, pTbIcons);
        Marshal.FreeHGlobal(pTbIcons);
    }

    internal static void PluginCleanUp() => Preferences.Serialize(iniFilePath);

    internal static void ToogleCompress()
    {
        IntPtr bufferId = nppGateway.GetCurrentBufferId();
        CompressionSettings compressor = Preferences.GetNextCompressor(fileTracker.GetCompressor(bufferId)?.CompressionAlgorithm, Preferences.GetCompressionBySuffix(nppGateway.GetFullPathFromBufferId(bufferId)));

        SetCompression(bufferId, compressor);

        UpdateCommandChecked(bufferId);
        UpdateStatusbar(bufferId, true);
    }

    private static void SetCompression(IntPtr bufferId, CompressionSettings compressor)
    {
        if (null == compressor)
        {
            NppEncoding enc = NppGZipFileViewerHelper.ToNppEncoding(fileTracker.GetEncoding(bufferId) ?? new UTF8Encoding(false));
            fileTracker.Exclude(bufferId, nppGateway.GetFullPathFromBufferId(bufferId));
            nppGateway.SendMenuEncoding(enc);
            nppGateway.MakeCurrentBufferDirty();
        }
        else
        {
            Encoding encoding = fileTracker.GetEncoding(bufferId) ?? NppGZipFileViewerHelper.ResetEncoding();
            fileTracker.Include(bufferId, nppGateway.GetFullPathFromBufferId(bufferId), encoding, compressor);
            nppGateway.MakeCurrentBufferDirty();
        }
    }

    private static void SetCompression(string compressionAlgorithm)
    {
        IntPtr bufferId = nppGateway.GetCurrentBufferId();
        CompressionSettings compressor = Preferences.EnumerateCompressions().FirstOrDefault(alg => alg.CompressionAlgorithm == compressionAlgorithm);

        compressor = fileTracker.GetCompressor(bufferId) == compressor ? null : compressor; // if already compressed (same compressor) disable compression

        SetCompression(bufferId, compressor);


        UpdateCommandChecked(bufferId);
        UpdateStatusbar(bufferId, true);
    }
}