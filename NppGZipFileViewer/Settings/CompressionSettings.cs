using System;
using System.Collections.Generic;
using System.IO;

namespace NppGZipFileViewer.Settings;

[Serializable]
public abstract class CompressionSettings
{
    public List<string> Extensions { get; set; } = [];

    public abstract string CompressionAlgorithm { get; }

    public void Compress(Stream inStream, Stream outStream)
    {
        using Stream compressionStream = GetCompressionStream(outStream);
        inStream.CopyTo(compressionStream);
    }

    public void Decompress(Stream inStream, Stream outStream)
    {
        using Stream compressionStream = GetDecompressionStream(inStream);
        compressionStream.CopyTo(outStream);
    }

    public abstract Stream GetCompressionStream(Stream outStream);
    public abstract Stream GetDecompressionStream(Stream inStream);

    public override string ToString() => CompressionAlgorithm;

}
