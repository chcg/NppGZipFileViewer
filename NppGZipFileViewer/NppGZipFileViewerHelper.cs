using Kbg.NppPluginNET.PluginInfrastructure;
using NppGZipFileViewer.Settings;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace NppGZipFileViewer;

internal static class NppGZipFileViewerHelper
{
    internal static MemoryStream GetContentStream(ScNotification notification, StringBuilder path) => GetContentStream(notification.Header.IdFrom, path.ToString());

    private static MemoryStream GetContentStream(IntPtr idFrom, string path)
    {
        _ = Win32.SendMessage(PluginBase.nppData._nppHandle, (uint)NppMsg.NPPM_SWITCHTOFILE, idFrom, path);

        int data_length = (int)Win32.SendMessage(PluginBase.GetCurrentScintilla(), SciMsg.SCI_GETLENGTH, 0, 0);
        if (data_length <= 0)
            return new MemoryStream();

        IntPtr pData = Win32.SendMessage(PluginBase.GetCurrentScintilla(), SciMsg.SCI_GETCHARACTERPOINTER, 0, 0);
        if (pData == IntPtr.Zero)
            return new MemoryStream();
        MemoryStream memoryStream = new();
        memoryStream.SetLength(data_length);
        Marshal.Copy(pData, memoryStream.GetBuffer(), 0, data_length);
        return memoryStream;
    }

    internal static MemoryStream GetContentStream(ScNotification notification, string path) => GetContentStream(notification.Header.IdFrom, path);

    internal static MemoryStream GetCurrentContentStream()
    {

        int data_length = (int)Win32.SendMessage(PluginBase.GetCurrentScintilla(), SciMsg.SCI_GETLENGTH, 0, 0);
        if (data_length <= 0)
            return new MemoryStream();

        IntPtr pData = Win32.SendMessage(PluginBase.GetCurrentScintilla(), SciMsg.SCI_GETCHARACTERPOINTER, 0, 0);
        if (pData == IntPtr.Zero)
            return new MemoryStream();
        MemoryStream memoryStream = new();
        memoryStream.SetLength(data_length);
        Marshal.Copy(pData, memoryStream.GetBuffer(), 0, data_length);
        return memoryStream;
    }

    internal static MemoryStream Decode(Stream gzStream, CompressionSettings compression)
    {
        MemoryStream decodedStream = new();
        compression.Decompress(gzStream, decodedStream);
        return decodedStream;
    }

    internal static Encoding ToEncoding(NppEncoding nppEncoding) => nppEncoding switch
    {
        NppEncoding.UTF16_LE => new UnicodeEncoding(false, true),
        NppEncoding.UTF8_BOM => new UTF8Encoding(true),
        NppEncoding.ANSI => new ASCIIEncoding(),
        NppEncoding.UTF16_BE => new UnicodeEncoding(true, true),
        _ => new UTF8Encoding(false),
    };

    internal static Encoding SetDecodedText(MemoryStream decodedContentStream)
    {
        _ = new NotepadPPGateway();
        ScintillaGateway scintillaGateway = new(PluginBase.GetCurrentScintilla());

        //var encoding = nppGateway.GetBufferEncoding(nppGateway.GetCurrentBufferId());

        decodedContentStream.Position = 0;
        byte[] bom = new byte[Math.Min(4, decodedContentStream.Length)];

        _ = decodedContentStream.Read(bom, 0, bom.Length);
        decodedContentStream.Position = 0;
        Encoding srcEncoding = BOMDetector.GetEncoding(bom) switch
        {
            BOM.UTF8 => new UTF8Encoding(true),
            BOM.UTF16LE => new UnicodeEncoding(false, true),
            BOM.UTF16BE => new UnicodeEncoding(true, true),
            _ => new UTF8Encoding(),
        };
        byte[] buffer = Encoding.Convert(srcEncoding, new UTF8Encoding(false), decodedContentStream.GetBuffer(), 0, (int)decodedContentStream.Length);

        GCHandle pinnedArray = GCHandle.Alloc(buffer, GCHandleType.Pinned);

        _ = Win32.SendMessage(PluginBase.GetCurrentScintilla(), SciMsg.SCI_CLEARALL, 0, 0);
        scintillaGateway.SetCodePage(65001);
        _ = Win32.SendMessage(PluginBase.GetCurrentScintilla(), SciMsg.SCI_ADDTEXT, buffer.Length, pinnedArray.AddrOfPinnedObject());
        pinnedArray.Free();
        return srcEncoding;
    }

    internal static NppEncoding ToNppEncoding(Encoding encoding) => encoding?.CodePage switch
    {
        // UTF-8
        65001 => encoding.GetPreamble().Length == 0 ? NppEncoding.UTF8 : NppEncoding.UTF8_BOM,
        // utf-16be
        1201 => NppEncoding.UTF16_BE,
        // utf-16le
        1200 => NppEncoding.UTF16_LE,
        // iso-8859-1
        1252 => NppEncoding.ANSI,
        // default
        _ => NppEncoding.UTF8,
    };

    internal static Encoding ResetEncoding()
    {
        NotepadPPGateway gateway = new();
        ScintillaGateway scintillaGateway = new(PluginBase.GetCurrentScintilla());
        IntPtr bufferID = gateway.GetCurrentBufferId();
        long nppEnc = gateway.GetBufferEncoding(bufferID);
        Encoding encoding = ToEncoding((NppEncoding)nppEnc);
        scintillaGateway.SetCodePage(65001);
        gateway.SendMenuEncoding(NppEncoding.UTF8);
        return encoding;
    }

    internal static void SetEncodedText(MemoryStream encodedContentStream)
    {
        _ = new NotepadPPGateway();
        ScintillaGateway scintillaGateway = new(PluginBase.GetCurrentScintilla());
        GCHandle pinnedArray = GCHandle.Alloc(encodedContentStream.GetBuffer(), GCHandleType.Pinned);

        scintillaGateway.ClearAll();
        _ = Win32.SendMessage(PluginBase.GetCurrentScintilla(), SciMsg.SCI_ADDTEXT, (int)encodedContentStream.Length, pinnedArray.AddrOfPinnedObject());
        pinnedArray.Free();
    }

    internal static MemoryStream Encode(Stream stream, Encoding dstEncoding, CompressionSettings compression)
    {
        ScintillaGateway scintillaGateway = new(PluginBase.GetCurrentScintilla());
        MemoryStream encodedStream = new();
        using Stream compressionStream = compression.GetCompressionStream(encodedStream);

        Encoding srcEncoding = Encoding.GetEncoding(scintillaGateway.GetCodePage());


        if (srcEncoding == dstEncoding)
            stream.CopyTo(compressionStream);
        else
        {
            using MemoryStream mem = new();
            stream.CopyTo(mem);
            byte[] buffer = Encoding.Convert(srcEncoding, dstEncoding, mem.GetBuffer(), 0, (int)mem.Length);
            if (dstEncoding != new UTF8Encoding(false))
            {
                byte[] preamble = dstEncoding.GetPreamble();
                compressionStream.Write(preamble, 0, preamble.Length);
            }
            compressionStream.Write(buffer, 0, buffer.Length);
        }
        return encodedStream;
    }
}
