using NppGZipFileViewer.Settings;
using System;
using System.Collections.Generic;
using System.Text;

namespace NppGZipFileViewer;

public class FileTracker
{
    private readonly HashSet<IntPtr> zippedFiles = [];
    private readonly Dictionary<IntPtr, string> filePathes = [];
    private readonly Dictionary<IntPtr, Encoding> encodings = [];
    private readonly Dictionary<IntPtr, CompressionSettings> compression = [];
    private readonly HashSet<IntPtr> excludedFiles = [];
    public void Include(IntPtr id, StringBuilder path, Encoding encoding, CompressionSettings compressor) => Include(id, path.ToString(), encoding, compressor);

    public void Exclude(IntPtr id, StringBuilder path) => Exclude(id, path.ToString());

    public void Include(IntPtr id, string path, Encoding encoding, CompressionSettings compressor)
    {
        _ = excludedFiles.Remove(id);
        _ = zippedFiles.Add(id);
        if (encodings.ContainsKey(id))
            encodings[id] = encoding;
        else encodings.Add(id, encoding);
        if (!filePathes.ContainsKey(id))
            filePathes.Add(id, path);
        else filePathes[id] = path;
        if (!compression.ContainsKey(id))
            compression.Add(id, compressor);
        else compression[id] = compressor;

    }
    public void Exclude(IntPtr id, string path)
    {
        _ = zippedFiles.Remove(id);
        _ = encodings.Remove(id);
        _ = excludedFiles.Add(id);
        _ = compression.Remove(id);
        if (!filePathes.ContainsKey(id))
            filePathes.Add(id, path);
        else filePathes[id] = path;
    }


    public void Remove(IntPtr id)
    {
        _ = zippedFiles.Remove(id);
        _ = excludedFiles.Remove(id);
        _ = filePathes.Remove(id);
        _ = encodings.Remove(id);
    }

    public bool IsIncluded(IntPtr id) => zippedFiles.Contains(id);

    public bool IsExcluded(IntPtr id) => excludedFiles.Contains(id);

    public string GetStoredPath(IntPtr id) { _ = filePathes.TryGetValue(id, out string path); return path; }

    public CompressionSettings GetCompressor(IntPtr id) => compression.TryGetValue(id, out CompressionSettings comp) ? comp : null;

    public Encoding GetEncoding(IntPtr id) => encodings.TryGetValue(id, out Encoding encoding) ? encoding : null;

}
