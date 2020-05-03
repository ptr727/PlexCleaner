using System.IO;
using System.IO.Compression;
using System.Text;

namespace PlexCleaner
{
    // https://stackoverflow.com/questions/7343465/compression-decompression-string-with-c-sharp

    public static class StringCompression
    {
        public static string Compress(string uncompressedString)
        {
            byte[] compressedBytes;
            using MemoryStream uncompressedStream = new MemoryStream(Encoding.UTF8.GetBytes(uncompressedString));
            {
                using MemoryStream compressedStream = new MemoryStream();
                {
                    using DeflateStream compressorStream = new DeflateStream(compressedStream, CompressionMode.Compress);
                    {
                        uncompressedStream.CopyTo(compressorStream);
                    }
                }
                compressedBytes = compressedStream.ToArray();
            }

            return System.Convert.ToBase64String(compressedBytes);
        }

        public static string Decompress(string compressedString)
        {
            byte[] decompressedBytes;
            using MemoryStream compressedStream = new MemoryStream(System.Convert.FromBase64String(compressedString));
            {
                using DeflateStream decompressorStream = new DeflateStream(compressedStream, CompressionMode.Decompress);
                {
                    using MemoryStream decompressedStream = new MemoryStream();
                    {
                        decompressorStream.CopyTo(decompressedStream);
                        decompressedBytes = decompressedStream.ToArray();
                    }
                }
            }

            return Encoding.UTF8.GetString(decompressedBytes);
        }
    }
}

public static class Extensions
{
    public static string Compress(this string uncompressedString)
    {
        return PlexCleaner.StringCompression.Compress(uncompressedString);
    }

    public static string Decompress(this string compressedString)
    {
        return PlexCleaner.StringCompression.Decompress(compressedString);
    }
}
