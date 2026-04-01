using Shared.ByteParsing;
using Shared.Core.PackFiles.Utility;

namespace Shared.Core.PackFiles.Models
{
    public class PackedFileSourceParent
    {
        public required string FilePath { get; set; }

        private FileStream _sharedStream;
        private readonly object _streamLock = new();

        /// <summary>
        /// Read raw bytes from the cached FileStream (thread-safe).
        /// The stream is lazily opened and reused across calls.
        /// </summary>
        internal byte[] ReadFromSharedStream(long offset, int size)
        {
            lock (_streamLock)
            {
                if (_sharedStream == null || !_sharedStream.CanRead)
                {
                    _sharedStream?.Dispose();
                    _sharedStream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                }
                var data = new byte[size];
                _sharedStream.Seek(offset, SeekOrigin.Begin);
                _sharedStream.ReadExactly(data, 0, size);
                return data;
            }
        }

        /// <summary>
        /// Close the cached FileStream so the file can be deleted/overwritten.
        /// The stream will be lazily reopened on next read.
        /// </summary>
        internal void CloseStream()
        {
            lock (_streamLock)
            {
                _sharedStream?.Dispose();
                _sharedStream = null;
            }
        }
    }

    public record PackedFileSource : IDataSource
    {
        public long Offset { get; private set; }
        public long Size { get; private set; }
        public bool IsEncrypted { get; private set; }
        public bool IsCompressed { get; set; }
        public CompressionFormat CompressionFormat { get; set; }
        public uint UncompressedSize { get; set; }
        public PackedFileSourceParent Parent { get; set; }

        public PackedFileSource(
            PackedFileSourceParent parent,
            long offset,
            long length,
            bool isEncrypted,
            bool isCompressed,
            CompressionFormat compressionFormat,
            uint uncompressedSize)
        {
            Offset = offset;
            Parent = parent;
            Size = length;
            IsEncrypted = isEncrypted;
            IsCompressed = isCompressed;
            CompressionFormat = compressionFormat;
            UncompressedSize = uncompressedSize;
        }

        public byte[] ReadData()
        {
            var data = Parent.ReadFromSharedStream(Offset, (int)Size);

            if (IsEncrypted)
                data = FileEncryption.Decrypt(data);

            if (IsCompressed)
            {
                data = FileCompression.Decompress(data, (int)UncompressedSize, CompressionFormat);
                if (data.Length != UncompressedSize)
                    throw new InvalidDataException($"Decompressed bytes {data.Length:N0} does not match the expected uncompressed bytes {UncompressedSize:N0}.");
            }

            return data;
        }

        public byte[] ReadData(Stream knownStream)
        {
            var data = new byte[Size];
            knownStream.Seek(Offset, SeekOrigin.Begin);
            knownStream.ReadExactly(data, 0, (int)Size);

            if (IsEncrypted)
                data = FileEncryption.Decrypt(data);

            if (IsCompressed)
            {
                data = FileCompression.Decompress(data, (int)UncompressedSize, CompressionFormat);
                if (data.Length != UncompressedSize)
                    throw new InvalidDataException($"Decompressed bytes {data.Length:N0} does not match the expected uncompressed bytes {UncompressedSize:N0}.");
            }

            return data;
        }


        public byte[] PeekData(int size)
        {
            byte[] data;

            if (!IsEncrypted && !IsCompressed)
            {
                data = Parent.ReadFromSharedStream(Offset, size);
            }
            else
            {
                data = Parent.ReadFromSharedStream(Offset, (int)Size);

                if (IsEncrypted)
                    data = FileEncryption.Decrypt(data);

                if (IsCompressed)
                {
                    data = FileCompression.Decompress(data, size, CompressionFormat);
                    if (data.Length != size)
                        throw new InvalidDataException($"Decompressed bytes {data.Length:N0} does not match the expected uncompressed bytes {size:N0}.");
                }
            }

            return data;
        }

       

        public byte[] ReadDataWithoutDecompressing()
        {
            var data = Parent.ReadFromSharedStream(Offset, (int)Size);

            if (IsEncrypted)
                data = FileEncryption.Decrypt(data);

            return data;
        }

        public ByteChunk ReadDataAsChunk() => new ByteChunk(ReadData());
    }
}
