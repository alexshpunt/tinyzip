using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

public class TinyZip : IDisposable
{
    public class FileDescription
    {
        public enum CompressionMethod
        {
            None = 0,
            Deflate = 8,
            Deflate64 = 9,
            BZip2 = 12,
            LZMA = 14,
            WavPack = 97,
            PPMd = 98,
            Unsupported = -1
        };

        public CompressionMethod compressionMethod;

        public byte[] data;
        public int compressedSize;
        public int uncompressedSize;
    }

    public List<string> FileNames
    {
        get
        {
            var keys = new string[mRecordsOffsets.Keys.Count];
            mRecordsOffsets.Keys.CopyTo(keys, 0);
            return new List<string>(keys);
        }
    }
    public FileDescription this[string key]
    {
        get
        {
            var offset = 0;
            if (mReleased || !mRecordsOffsets.TryGetValue(key, out offset))
            {
                throw new System.Exception(string.Format("There is no {0} file in the archive.", key));
            }

            return ReadLocalFileHeader(offset);
        }
    }

    public TinyZip(string path)
    {
        mStream = File.Open(path, FileMode.Open);
        Initilize();
    }
    public TinyZip(byte[] bytes)
    {
        mStream = new MemoryStream(bytes);
        Initilize();
    }

    public void Dispose()
    {
        if (!mReleased)
        {
            mReleased = true;

            mReader.Close();
            mReader = null;

            mStream.Close();
            mStream = null;

            mRecordsOffsets.Clear();
            mRecordsOffsets = null;
        }
    }

    private enum Signatures
    {
        EndOfCentralDirectoryRecord = 0x06054b50,
        CentralDirectoryFileHeader = 0x02014b50,
        LocalFileHeader = 0x04034b50
    }

    private void Initilize()
    {
        mReleased = false;

        mReader = new BinaryReader(mStream);
        mRecordsOffsets = new Dictionary<string, int>();

        var eocdPos = FindEndOfCentralDirectory();
        if (eocdPos == -1)
        {
            throw new Exception("Corrupted EOCD signature.");
        }
        int recordsNumber = -1;
        var cdfhPos = FindCentralDirectoryFileHeader(eocdPos, out recordsNumber);
        ReadCentralDirectoryFileHeader(cdfhPos, recordsNumber);
    }

    private long FindEndOfCentralDirectory()
    {
        var step = sizeof(int);
        var pos = mStream.Seek(-step, SeekOrigin.End);
        do
        {
            var signature = mReader.ReadInt32();
            if (signature == (int)Signatures.EndOfCentralDirectoryRecord)
            {
                return pos;
            }
            if (pos <= step)
            {
                break;
            }
            pos = mStream.Seek(-(step + 1), SeekOrigin.Current);
        }
        while (true);
        return -1;
    }
    private long FindCentralDirectoryFileHeader(long eocdPos, out int recordsNumber)
    {
        const int totalNumberOfRecordsOffset = 10;

        mStream.Seek(eocdPos + totalNumberOfRecordsOffset, SeekOrigin.Begin);
        recordsNumber = mReader.ReadInt16();

        var sizeOfCentralDirectory = mReader.ReadInt32();
        var centralDirectoryOffset = mReader.ReadInt32();
        return centralDirectoryOffset;
    }

    private void ReadCentralDirectoryFileHeader(long cdfhPos, int recordsNumber)
    {
        const int fileNameLengthOffset = 28;
        const int recordPositionOffset = 42;

        var pos = cdfhPos;
        for (int i = 0; i < recordsNumber; i++)
        {
            mStream.Seek(pos, SeekOrigin.Begin);

            var signature = mReader.ReadInt32();
            if (signature != (int)Signatures.CentralDirectoryFileHeader)
            {
                throw new Exception("Corrupted CDFH signature.");
            }

            mStream.Seek(pos + fileNameLengthOffset, SeekOrigin.Begin);
            var fileNameLength = mReader.ReadInt16();
            var extraFieldLength = mReader.ReadInt16();
            var commentLength = mReader.ReadInt16();

            mStream.Seek(pos + recordPositionOffset, SeekOrigin.Begin);
            var recordOffset = mReader.ReadInt32();

            var fileName = Encoding.ASCII.GetString(mReader.ReadBytes(fileNameLength));
            var notFolder = !(fileName.EndsWith("\\") || fileName.EndsWith("/"));

            if (notFolder)
            {
                mRecordsOffsets[fileName] = recordOffset;
            }

            pos = mStream.Seek(extraFieldLength + commentLength, SeekOrigin.Current);
        }
    }
    private FileDescription ReadLocalFileHeader(long lfhPos)
    {
        const int compressedSizeOffset = 18;

        mStream.Seek(lfhPos, SeekOrigin.Begin);
        var signature = mReader.ReadInt32();
        if (signature != (int)Signatures.LocalFileHeader)
        {
            throw new Exception("Corrupted LFH signature.");
        }
        var minVersion = mReader.ReadInt16();
        var bitFlag = mReader.ReadInt16();

        var encrypted = (bitFlag & 1) == 1;
        if (encrypted)
        {
            throw new NotSupportedException("Encrypted files are not supported.");
        }

        var compressionMethod = mReader.ReadInt16();
        var fileDescription = new FileDescription();
        switch (compressionMethod)
        {
            case (short)FileDescription.CompressionMethod.Deflate:
            case (short)FileDescription.CompressionMethod.Deflate64:
            case (short)FileDescription.CompressionMethod.LZMA:
            case (short)FileDescription.CompressionMethod.BZip2:
            case (short)FileDescription.CompressionMethod.PPMd:
            case (short)FileDescription.CompressionMethod.WavPack:
            case (short)FileDescription.CompressionMethod.None:
                fileDescription.compressionMethod = (FileDescription.CompressionMethod)compressionMethod;
                break;
            default:
                fileDescription.compressionMethod = FileDescription.CompressionMethod.Unsupported;
                break;
        }

        mStream.Seek(lfhPos + compressedSizeOffset, SeekOrigin.Begin);
        fileDescription.compressedSize = mReader.ReadInt32();
        fileDescription.uncompressedSize = mReader.ReadInt32();

        var recordNameLength = mReader.ReadInt16();
        var extraFieldLength = mReader.ReadInt16();
        mStream.Seek(recordNameLength + extraFieldLength, SeekOrigin.Current);
        if (fileDescription.compressionMethod != FileDescription.CompressionMethod.None)
        {
            fileDescription.data = mReader.ReadBytes(fileDescription.compressedSize);
        }
        else
        {
            fileDescription.data = mReader.ReadBytes(fileDescription.uncompressedSize);
        }

        return fileDescription;
    }

    private bool mReleased;
    private Stream mStream;
    private BinaryReader mReader;
    private Dictionary<string, int> mRecordsOffsets;
}