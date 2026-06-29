using System.IO.Compression;
using System.Text;

namespace Optimus.Utilities;

public static class CompressionUtilities
{
    public static string DecompressBrotli(byte[] compressedBytes)
    {
        using var inputStream = new MemoryStream(compressedBytes);
        using var decompressionStream = new BrotliStream(inputStream, CompressionMode.Decompress);
        using var reader = new StreamReader(decompressionStream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
