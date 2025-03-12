using Standart.Hash.xxHash;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace InfBinRepackager
{
    public class InfFile
    {
        public const int BlockSize = 1024 * 1024;

        public byte[] Checksum;
        public int InfLength;
        public int Version__;
        public DateTime CreationDate;
        public DateTime UpdateDate;
        public int FileCount;
        public int DataBlockCount;
        public int FileStructureLength;
        public string FilingMode__;
        public string FileStructure;
        public List<FileData> Files;
        public List<Block> Blocks;

        public InfFile()
        {
            Checksum = new byte[4];
            Version__ = 11;
            CreationDate = DateTime.Now;
            UpdateDate = DateTime.Now;
            FilingMode__ = "FULL";
            FileStructure = string.Empty;
            Files = new List<FileData>();
            Blocks = new List<Block>();
        }

        public InfFile(byte[] data) : this()
        {
            Read(data);
        }

        public void Read(byte[] data)
        {
            Stream stream = new MemoryStream(data);

            byte[] buffer = new byte[data.Length];

            stream.Read(Checksum, 0, 4);

            stream.Read(buffer, 0, 4);
            InfLength = BitConverter.ToInt32(buffer, 0);

            stream.Read(buffer, 0, 4);
            Version__ = BitConverter.ToInt32(buffer, 0);

            stream.Read(buffer, 0, 20);
            CreationDate = Convert.ToDateTime(Encoding.UTF8.GetString(buffer, 0, 20));

            stream.Read(buffer, 0, 20);
            UpdateDate = Convert.ToDateTime(Encoding.UTF8.GetString(buffer, 0, 20));

            stream.Read(buffer, 0, 4);
            FileCount = BitConverter.ToInt32(buffer, 0);

            stream.Read(buffer, 0, 4);
            DataBlockCount = BitConverter.ToInt32(buffer, 0);

            stream.Read(buffer, 0, 4);
            FileStructureLength = BitConverter.ToInt32(buffer, 0);

            stream.Read(buffer, 0, 4);
            FilingMode__ = Encoding.UTF8.GetString(buffer, 0, 4);

            stream.Read(buffer, 0, 6); //End of header

            stream.Read(buffer, 0, 2); //Beginning of file structure

            stream.Read(buffer, 0, FileStructureLength);
            FileStructure = Encoding.UTF8.GetString(buffer, 0, FileStructureLength);

            for (int i = 0; i < FileCount; i++)
            {
                FileData file = new FileData();

                stream.Read(buffer, 0, 4);
                file.PathOffset = BitConverter.ToInt32(buffer, 0);

                for (int j = file.PathOffset; j < FileStructureLength; j++)
                {
                    if (FileStructure[j] == '\0')
                        break;

                    file.Path += FileStructure[j];
                }

                stream.Read(file.Id, 0, 4);

                stream.Read(buffer, 0, 4);
                file.UncompressedSize = BitConverter.ToInt32(buffer, 0);

                stream.Read(buffer, 0, 4);
                file.CompressedSize = BitConverter.ToInt32(buffer, 0);

                stream.Read(buffer, 0, 4);
                file.Category = BitConverter.ToInt32(buffer, 0);

                stream.Read(buffer, 0, 4);
                file.Index = BitConverter.ToInt32(buffer, 0);

                stream.Read(buffer, 0, 4);
                file.PartitionCount = BitConverter.ToInt32(buffer, 0);

                Files.Add(file);
            }

            for (int i = 0; i < DataBlockCount; i++)
            {
                Block block = new Block();

                stream.Read(block.Id, 0, 4);

                block.Index = i;

                stream.Read(buffer, 0, 4);
                block.FileCount = BitConverter.ToInt32(buffer, 0);

                for (int j = 0; j < block.FileCount; j++)
                {
                    BlockPartition partition = new BlockPartition();

                    partition.BlockIndex = block.Index; //Redundant index for quicker access from FileData

                    stream.Read(buffer, 0, 4);
                    partition.Offset = BitConverter.ToInt32(buffer, 0);

                    stream.Read(buffer, 0, 4);
                    partition.Length = BitConverter.ToInt32(buffer, 0);

                    stream.Read(buffer, 0, 4);
                    partition.FileIndex = BitConverter.ToInt32(buffer, 0);

                    stream.Read(buffer, 0, 4);
                    partition.FilePart = BitConverter.ToInt32(buffer, 0);

                    block.Partitions.Add(partition);

                    Files.First(x => x.Index == partition.FileIndex).BlockPartitions.Add(partition);
                }

                Blocks.Add(block);
            }
        }

        public byte[] Write()
        {
            byte[] dataInf = new byte[InfLength];

            int position = 0;

            Checksum.CopyTo(dataInf, position);
            position += 4;

            BitConverter.GetBytes(InfLength).CopyTo(dataInf, position);
            position += 4;

            BitConverter.GetBytes(Version__).CopyTo(dataInf, position);
            position += 4;

            Encoding.UTF8.GetBytes(CreationDate.ToString("yyyy'-'MM'-'dd HH':'mm':'ss")).CopyTo(dataInf, position);
            position += 20;

            Encoding.UTF8.GetBytes(UpdateDate.ToString("yyyy'-'MM'-'dd HH':'mm':'ss")).CopyTo(dataInf, position);
            position += 20;

            BitConverter.GetBytes(FileCount).CopyTo(dataInf, position);
            position += 4;

            BitConverter.GetBytes(DataBlockCount).CopyTo(dataInf, position);
            position += 4;

            BitConverter.GetBytes(FileStructureLength).CopyTo(dataInf, position);
            position += 4;

            Encoding.UTF8.GetBytes(FilingMode__).CopyTo(dataInf, position);
            position += 4;

            Array.Copy(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 0, dataInf, position, 6); //End of header
            position += 6;

            Array.Copy(new byte[] { 0x73, 0x2F }, 0, dataInf, position, 2); //Beginning of file structure
            position += 2;

            Encoding.UTF8.GetBytes(FileStructure).CopyTo(dataInf, position);
            position += FileStructure.Length;

            foreach (FileData file in Files)
            {
                BitConverter.GetBytes(file.PathOffset).CopyTo(dataInf, position);
                position += 4;

                file.Id.CopyTo(dataInf, position);
                position += 4;

                BitConverter.GetBytes(file.UncompressedSize).CopyTo(dataInf, position);
                position += 4;

                BitConverter.GetBytes(file.CompressedSize).CopyTo(dataInf, position);
                position += 4;

                BitConverter.GetBytes(file.Category).CopyTo(dataInf, position);
                position += 4;

                BitConverter.GetBytes(file.Index).CopyTo(dataInf, position);
                position += 4;

                BitConverter.GetBytes(file.PartitionCount).CopyTo(dataInf, position);
                position += 4;
            }

            foreach (Block block in Blocks)
            {
                block.Id.CopyTo(dataInf, position);
                position += 4;

                BitConverter.GetBytes(block.FileCount).CopyTo(dataInf, position);
                position += 4;

                foreach (BlockPartition partition in block.Partitions)
                {
                    BitConverter.GetBytes(partition.Offset).CopyTo(dataInf, position);
                    position += 4;

                    BitConverter.GetBytes(partition.Length).CopyTo(dataInf, position);
                    position += 4;

                    BitConverter.GetBytes(partition.FileIndex).CopyTo(dataInf, position);
                    position += 4;

                    BitConverter.GetBytes(partition.FilePart).CopyTo(dataInf, position);
                    position += 4;
                }
            }

            return dataInf;
        }

        public List<string> Print()
        {
            List<string> lines = new List<string>()
            {
                nameof(Checksum) + "\t\t" + BitConverter.ToString(Checksum),
                nameof(InfLength) + "\t\t" + InfLength,
                nameof(Version__) + "\t\t" + Version__,
                nameof(CreationDate) + "\t\t" + CreationDate.ToString("yyyy'-'MM'-'dd HH':'mm':'ss"),
                nameof(UpdateDate) + "\t\t" + UpdateDate.ToString("yyyy'-'MM'-'dd HH':'mm':'ss"),
                nameof(FileCount) + "\t\t" + FileCount,
                nameof(DataBlockCount) + "\t\t" + DataBlockCount,
                nameof(FileStructureLength) + "\t" + FileStructureLength,
                nameof(FilingMode__) + "\t\t" + FilingMode__,
            };

            lines.Add("");

            foreach (FileData file in Files)
            {
                lines.Add("");
                lines.AddRange(file.Print());
            }

            lines.Add("");

            foreach (Block block in Blocks)
            {
                lines.Add("");
                lines.AddRange(block.Print());
            }

            return lines.Select(x => x.Replace("__", "?")).ToList();
        }

        public FilePosition FindFreeSpace(int fileSize)
        {
            int currentIndex = 0;
            int currentOffset = 0;
            int freeSpace = 0;

            if (fileSize >= BlockSize)
            {
                for (int i = 0; i < Blocks.Count; i++)
                {
                    Block block = Blocks[i];

                    if (block.Partitions.Count > 0)
                    {
                        freeSpace += block.Partitions[0].Offset;

                        if (fileSize <= freeSpace)
                            return new FilePosition(currentIndex, currentOffset);

                        currentIndex = i;
                        currentIndex++;
                        freeSpace = 0;
                    }
                    else
                    {
                        freeSpace += BlockSize;

                        if (fileSize <= freeSpace)
                            return new FilePosition(currentIndex, currentOffset);
                    }
                }
            }
            else
            {
                for (int i = 0; i < Blocks.Count; i++)
                {
                    currentIndex = i;
                    currentOffset = 0;
                    freeSpace = 0;

                    Block block = Blocks[i];

                    for (int j = 0; j < block.Partitions.Count; j++)
                    {
                        freeSpace = block.Partitions[j].Offset - currentOffset;

                        if (fileSize <= freeSpace)
                            return new FilePosition(currentIndex, currentOffset);

                        currentOffset = block.Partitions[j].Offset + block.Partitions[j].Length;
                    }

                    freeSpace = BlockSize - currentOffset;

                    if (fileSize <= freeSpace)
                        return new FilePosition(currentIndex, currentOffset);
                }
            }

            return new FilePosition(Blocks.Count, 0);
        }

        public void Update(uint seed)
        {
            FileStructure = "";
            byte[] emptyHash = new byte[] { 0x00, 0x00, 0x00, 0x00 };

            foreach (FileData file in Files)
            {
                FileStructure += file.Path + (char)0x00;

                file.PartitionCount = file.BlockPartitions.Count;

                if (file.Id.SequenceEqual(emptyHash)) //Generate a new Id for new files
                {
                    byte[] fileBytes = new byte[6 * 4];
                    int position = 0;

                    BitConverter.GetBytes(file.PathOffset).CopyTo(fileBytes, position);
                    position += 4;

                    BitConverter.GetBytes(file.UncompressedSize).CopyTo(fileBytes, position);
                    position += 4;

                    BitConverter.GetBytes(file.CompressedSize).CopyTo(fileBytes, position);
                    position += 4;

                    BitConverter.GetBytes(file.Category).CopyTo(fileBytes, position);
                    position += 4;

                    BitConverter.GetBytes(file.Index).CopyTo(fileBytes, position);
                    position += 4;

                    BitConverter.GetBytes(file.PartitionCount).CopyTo(fileBytes, position);

                    file.Id = BitConverter.GetBytes(xxHash32.ComputeHash(fileBytes, 0, fileBytes.Length, seed));
                }
            }

            int partitionCount = 0;

            foreach (Block block in Blocks)
            {
                partitionCount += block.Partitions.Count;

                if (block.Id.SequenceEqual(emptyHash)) //Generate a new Id for new blocks
                {
                    byte[] blockBytes = new byte[4 + 4 * 4 * block.Partitions.Count];
                    int position = 0;

                    BitConverter.GetBytes(block.FileCount).CopyTo(blockBytes, position);
                    position += 4;

                    foreach (BlockPartition partition in block.Partitions)
                    {
                        BitConverter.GetBytes(partition.Offset).CopyTo(blockBytes, position);
                        position += 4;

                        BitConverter.GetBytes(partition.Length).CopyTo(blockBytes, position);
                        position += 4;

                        BitConverter.GetBytes(partition.FileIndex).CopyTo(blockBytes, position);
                        position += 4;

                        BitConverter.GetBytes(partition.FilePart).CopyTo(blockBytes, position);
                        position += 4;
                    }

                    block.Id = BitConverter.GetBytes(xxHash32.ComputeHash(blockBytes, 0, blockBytes.Length, seed));
                }
            }

            InfLength = 76 + FileStructure.Length + 7 * 4 * Files.Count + 2 * 4 * Blocks.Count + 4 * 4 * partitionCount;
            UpdateDate = DateTime.Now;
            FileCount = Files.Count;
            DataBlockCount = Blocks.Count;
            FileStructureLength = FileStructure.Length;

            byte[] bytes = Write();

            Checksum = BitConverter.GetBytes(xxHash32.ComputeHash(bytes, 4, bytes.Length - 4, seed));
        }
    }

    public class FileData
    {
        public int PathOffset;
        public string Path;
        public byte[] Id;
        public int UncompressedSize;
        public int CompressedSize;
        public int Category;
        public int Index;
        public int PartitionCount;
        public List<BlockPartition> BlockPartitions;

        public FileData()
        {
            Path = string.Empty;
            Id = new byte[4];
            Category = 1;
            PartitionCount = 1;
            BlockPartitions = new List<BlockPartition>();
        }

        public List<string> Print()
        {
            List<string> lines = new List<string>()
            {
                nameof(PathOffset) + "\t\t" + PathOffset,
                nameof(Path) + "\t\t\t" + Path,
                nameof(Id) + "\t\t\t" + BitConverter.ToString(Id),
                nameof(UncompressedSize) + "\t" + UncompressedSize,
                nameof(CompressedSize) + "\t\t" + CompressedSize,
                nameof(Category) + "\t\t" + Category,
                nameof(Index) + "\t\t\t" + Index,
                nameof(PartitionCount) + "\t\t" + PartitionCount,
            };

            return lines;
        }
    }

    public class Block
    {
        public byte[] Id;
        public int Index;
        public int FileCount;
        public List<BlockPartition> Partitions;

        public Block()
        {
            Id = new byte[4];
            Partitions = new List<BlockPartition>();
        }

        public List<string> Print()
        {
            List<string> lines = new List<string>()
            {
                nameof(Id) + "\t\t\t" + BitConverter.ToString(Id),
                nameof(Index) + "\t\t\t" + Index,
                nameof(FileCount) + "\t\t" + FileCount
            };

            foreach (BlockPartition partition in Partitions)
                lines.AddRange(partition.Print());

            return lines;
        }
    }

    public class BlockPartition
    {
        public int BlockIndex; //Redundant index for quicker access from FileData
        public int Offset;
        public int Length;
        public int FileIndex;
        public int FilePart;

        public List<string> Print()
        {
            List<string> lines = new List<string>()
            {
                "\t" + nameof(Offset) + "\t\t" + Offset,
                "\t" + nameof(Length) + "\t\t" + Length,
                "\t" + nameof(FileIndex) + "\t" + FileIndex,
                "\t" + nameof(FilePart) + "\t" + FilePart
            };

            return lines;
        }
    }

    public struct FilePosition
    {
        public int BlockIndex;
        public int BlockOffset;

        public FilePosition(int index, int offset)
        {
            BlockIndex = index;
            BlockOffset = offset;
        }
    }
}
