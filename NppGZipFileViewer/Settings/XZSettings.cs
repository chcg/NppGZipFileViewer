using Joveler.Compression.XZ;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace NppGZipFileViewer.Settings;


[Serializable]
public class XZSettings : CompressionSettings
{
    static XZSettings()
    {
        string currentDir = System.IO.Path.GetDirectoryName( System.Reflection.Assembly.GetExecutingAssembly().Location);
        string libDir = "";
        switch (RuntimeInformation.ProcessArchitecture)
        {
            case Architecture.X86:
                libDir = System.IO.Path.Combine(currentDir, "x86", "liblzma.dll");
                break;
            case Architecture.X64:
                libDir = System.IO.Path.Combine(currentDir, "x64", "liblzma.dll");
                break;
            case Architecture.Arm64:
                libDir = System.IO.Path.Combine(currentDir, "arm64", "liblzma.dll");
                break;
        }

        XZInit.GlobalInit(libDir);
    }

    public override string CompressionAlgorithm => "xz";

    public int BufferSize { get; set; } = 1024 * 1024;

    public LzmaCheck ChecksumType { get; set; }

    public bool ExtremeFlag { get; set; } = false;

    public LzmaCompLevel CompressionLevel { get; set; } = LzmaCompLevel.Default;

    private XZCompressOptions CompressionOptions => new()
    {
        BufferSize = BufferSize,
        Check = ChecksumType,
        ExtremeFlag = ExtremeFlag,
        LeaveOpen = true,
        Level = CompressionLevel,

    };
    private XZDecompressOptions DecompressOptions => new()
    {
        BufferSize = BufferSize,
        LeaveOpen = true,
    };

    public bool MultiThread { get; set; } = false;
    public ulong BlockSize { get; set; }
    public int Threads { get; set; } = Environment.ProcessorCount;

    private XZThreadedCompressOptions ThreadOptions => new() { BlockSize = BlockSize, Threads = Threads };
    private XZThreadedDecompressOptions ThreadDecompressOptions => new() { Threads = Threads };

    public override Stream GetCompressionStream(Stream outStream) => !MultiThread ? new XZStream(outStream, CompressionOptions) : (Stream)new XZStream(outStream, CompressionOptions, ThreadOptions);
    public override Stream GetDecompressionStream(Stream inStream) => !MultiThread ? new XZStream(inStream, DecompressOptions) : (Stream)new XZStream(inStream, DecompressOptions, ThreadDecompressOptions);
}
