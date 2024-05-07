using System;
using System.IO;

namespace NppGZipFileViewer.Settings;

[Serializable]
public class ZstdSettings : CompressionSettings
{
    public int CompressionLevel { get; set; } = 11;
    public int BufferSize { get; set; } = 1024 * 1024;

    public override string CompressionAlgorithm => "zstd";

    public override Stream GetCompressionStream(Stream outStream)
    {
        ZstdSharp.CompressionStream stream = new(outStream,CompressionLevel,BufferSize,true);
        return stream;
    }
    public override Stream GetDecompressionStream(Stream inStream)
    {
        ZstdSharp.DecompressionStream decompressionStream = new(inStream,BufferSize,true);
        return decompressionStream;
    }
}
