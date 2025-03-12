using K4os.Compression.LZ4;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading.Tasks;

namespace InfBinRepackager
{
    public class Repacker
    {
        public int Seed;
        public string InfPath;
        public string BinPath;
        public string OutputDirectory;
        public InfFile Inf;

        private string UncompressedDirectory;
        private string CompressedDirectory;

        public Repacker(int seed, string infPath, string binPath, string outputDirectory, bool readFile)
        {
            Seed = seed;
            InfPath = infPath;
            BinPath = binPath;
            OutputDirectory = outputDirectory;

            if (readFile)
                Inf = new InfFile(File.ReadAllBytes(infPath));
            else
                Inf = new InfFile();

            UncompressedDirectory = Path.Combine(outputDirectory, "uncompressed");
            CompressedDirectory = Path.Combine(outputDirectory, "compressed");
        }

        public void Export(bool keepCompressed)
        {
            Console.WriteLine("Exporting all files");

            int completion = 0;

            Directory.CreateDirectory(OutputDirectory);

            using (var memoryMap = MemoryMappedFile.CreateFromFile(BinPath, FileMode.OpenOrCreate, "mappeddata.bin"))
            {
                Parallel.ForEach(Inf.Files, file =>
                {
                    byte[] compressedFile = new byte[file.CompressedSize];

                    foreach (BlockPartition partition in file.BlockPartitions)
                    {
                        using (var accessor = memoryMap.CreateViewAccessor(partition.BlockIndex * InfFile.BlockSize, InfFile.BlockSize))
                        {
                            accessor.ReadArray(partition.Offset, compressedFile, partition.FilePart * InfFile.BlockSize, partition.Length);
                        }
                    }

                    if (keepCompressed)
                    {
                        Directory.CreateDirectory(Path.Combine(CompressedDirectory, Path.GetDirectoryName(file.Path)));
                        File.WriteAllBytes(Path.Combine(CompressedDirectory, file.Path), compressedFile);
                    }
                    else
                    {
                        var uncompressedFile = new byte[file.UncompressedSize];

                        LZ4Codec.Decode(compressedFile, 0, compressedFile.Length, uncompressedFile, 0, uncompressedFile.Length);
                        Directory.CreateDirectory(Path.Combine(UncompressedDirectory, Path.GetDirectoryName(file.Path)));
                        File.WriteAllBytes(Path.Combine(UncompressedDirectory, file.Path), uncompressedFile);
                    }

                    Console.Write(++completion * 100 / Inf.Files.Count + "%\r");
                });
            }

            Console.WriteLine("Export completed");
        }

        public void Export(bool keepCompressed, string filePath)
        {
            FileData file = Inf.Files.FirstOrDefault(x => x.Path == filePath);

            if (file == null)
            {
                Console.WriteLine("File \"" + filePath + "\" not found");
                Console.WriteLine("Export failed");
                return;
            }

            Console.WriteLine("Exporting \"" + filePath + "\"");

            Directory.CreateDirectory(OutputDirectory);

            using (var memoryMap = MemoryMappedFile.CreateFromFile(BinPath, FileMode.OpenOrCreate, "mappeddata.bin"))
            {
                byte[] compressedFile = new byte[file.CompressedSize];

                foreach (BlockPartition partition in file.BlockPartitions)
                {
                    using (var accessor = memoryMap.CreateViewAccessor(partition.BlockIndex * InfFile.BlockSize, InfFile.BlockSize))
                    {
                        accessor.ReadArray(partition.Offset, compressedFile, partition.FilePart * InfFile.BlockSize, partition.Length);
                    }
                }

                if (keepCompressed)
                {
                    Directory.CreateDirectory(Path.Combine(CompressedDirectory, Path.GetDirectoryName(file.Path)));
                    File.WriteAllBytes(Path.Combine(CompressedDirectory, file.Path), compressedFile);
                }
                else
                {
                    var uncompressedFile = new byte[file.UncompressedSize];

                    LZ4Codec.Decode(compressedFile, 0, compressedFile.Length, uncompressedFile, 0, uncompressedFile.Length);
                    Directory.CreateDirectory(Path.Combine(UncompressedDirectory, Path.GetDirectoryName(file.Path)));
                    File.WriteAllBytes(Path.Combine(UncompressedDirectory, file.Path), uncompressedFile);
                }
            }

            Console.WriteLine("Export completed");
        }

        public void Import(bool append, bool keepCompressed, int compressionLevel = 0)
        {
            if (keepCompressed)
                Console.WriteLine("Importing files from \"" + CompressedDirectory + "\"");
            else
                Console.WriteLine("Importing files from \"" + UncompressedDirectory + "\"");

            Directory.CreateDirectory(CompressedDirectory);
            Directory.CreateDirectory(UncompressedDirectory);

            if (!keepCompressed)
            {
                if (compressionLevel < 3)
                    compressionLevel = 0;
                else if (compressionLevel > 12)
                    compressionLevel = 12;

                Compress((LZ4Level)compressionLevel);
            }

            BuildDatabase(append);

            WriteInf();

            WriteBin(append);

            Console.WriteLine("Import completed");
        }

        public void Print()
        {
            Console.WriteLine("Printing into \"" + Path.Combine(OutputDirectory, Path.GetFileName(InfPath)) + ".txt\"");

            Directory.CreateDirectory(OutputDirectory);
            File.WriteAllLines(Path.Combine(OutputDirectory, Path.GetFileName(InfPath)) + ".txt", Inf.Print());

            Console.WriteLine("Printing completed");
        }

        public void Compress(LZ4Level level)
        {
            Console.WriteLine($"Compressing files from \"{UncompressedDirectory}\" to \"{CompressedDirectory}\" at compression level {level}");

            int completion = 0;
            string[] files = Directory.GetFiles(UncompressedDirectory, "*", SearchOption.AllDirectories);

            Parallel.ForEach(files, file =>
            {
                var uncompressedFile = File.ReadAllBytes(file);
                //There are instances where files are larger once compressed so we oversize the array
                var compressedFile = new byte[uncompressedFile.Length + 1024 * 1024];

                int compressedLength = LZ4Codec.Encode(uncompressedFile, 0, uncompressedFile.Length, compressedFile, 0, compressedFile.Length, level);

                Array.Resize(ref compressedFile, compressedLength);

                string relativePath = Path.GetRelativePath(UncompressedDirectory, file);

                Directory.CreateDirectory(Path.Combine(CompressedDirectory, Path.GetDirectoryName(relativePath)));

                File.WriteAllBytes(Path.Combine(CompressedDirectory, relativePath), compressedFile);

                Console.Write(++completion * 100 / files.Length + "%\r");
            });
        }

        public void BuildDatabase(bool append)
        {
            Console.WriteLine("Building Database");

            byte[] emptyHash = new byte[] { 0x00, 0x00, 0x00, 0x00 };

            if (append)
            {
                foreach (var file in Inf.Files)
                {
                    if (File.Exists(Path.Combine(CompressedDirectory, file.Path)))
                    {
                        file.BlockPartitions.Clear();
                        file.CompressedSize = (int)new FileInfo(Path.Combine(CompressedDirectory, file.Path)).Length;

                        if (File.Exists(Path.Combine(UncompressedDirectory, file.Path)))
                            file.UncompressedSize = (int)new FileInfo(Path.Combine(UncompressedDirectory, file.Path)).Length;

                        foreach (var block in Inf.Blocks)
                        {
                            if (block.Partitions.RemoveAll(x => x.FileIndex == file.Index) > 0)
                                block.FileCount = block.Partitions.Count;
                        }
                    }
                }
            }
            else
            {
                Inf.Files.Clear();
                Inf.Blocks.Clear();

                int fileIndex = 0;
                int pathOffset = 0;
                string[] files = Directory.GetFiles(CompressedDirectory, "*", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    string relativePath = Path.GetRelativePath(CompressedDirectory, file).Replace('\\', '/');

                    if (File.Exists(Path.Combine(UncompressedDirectory, relativePath)))
                    {
                        Inf.Files.Add(new FileData()
                        {
                            PathOffset = pathOffset,
                            Path = relativePath,
                            Id = emptyHash,
                            UncompressedSize = (int)new FileInfo(Path.Combine(UncompressedDirectory, relativePath)).Length,
                            CompressedSize = (int)new FileInfo(Path.Combine(CompressedDirectory, relativePath)).Length,
                            Index = fileIndex,
                            BlockPartitions = new List<BlockPartition>(),
                        });

                        fileIndex++;
                        pathOffset += relativePath.Length + 1;
                    }
                }
            }

            foreach (var file in Inf.Files.OrderByDescending(x => x.CompressedSize))
            {
                if (File.Exists(Path.Combine(CompressedDirectory, file.Path)))
                {
                    FilePosition position = Inf.FindFreeSpace(file.CompressedSize);

                    decimal partitionCount = Math.Ceiling(file.CompressedSize / Convert.ToDecimal(InfFile.BlockSize));

                    for (int i = 0; i < partitionCount; i++)
                    {
                        BlockPartition partition = new BlockPartition() 
                        { 
                            BlockIndex = position.BlockIndex + i, 
                            FilePart = i, FileIndex = file.Index, 
                            Offset = position.BlockOffset 
                        };

                        if (i == partitionCount - 1)
                            partition.Length = file.CompressedSize % InfFile.BlockSize;
                        else
                            partition.Length = InfFile.BlockSize;


                        if (Inf.Blocks.Exists(x => x.Index == position.BlockIndex + i))
                        {
                            Inf.Blocks[position.BlockIndex + i].Partitions.Add(partition);
                            Inf.Blocks[position.BlockIndex + i].Partitions = Inf.Blocks[position.BlockIndex + i].Partitions.OrderBy(x => x.Offset).ToList();
                            Inf.Blocks[position.BlockIndex + i].FileCount = Inf.Blocks[position.BlockIndex + i].Partitions.Count;
                            file.BlockPartitions.Add(partition);
                        }
                        else
                        {
                            Inf.Blocks.Add(new Block()
                            {
                                Partitions = new List<BlockPartition>() { partition },
                                Id = emptyHash,
                                Index = position.BlockIndex + i,
                                FileCount = 1
                            });
                            file.BlockPartitions.Add(partition);
                        }
                    }
                }
            }
        }

        public void WriteInf()
        {
            Console.WriteLine("Saving \".inf\" file to \"" + Path.Combine(OutputDirectory, Path.GetFileName(InfPath)) + "\"");

            Inf.Update((uint)Seed);
            File.WriteAllBytes(Path.Combine(OutputDirectory, Path.GetFileName(InfPath)), Inf.Write());
        }

        public void WriteBin(bool append)
        {
            Console.WriteLine("Saving \".bin\" file to \"" + Path.Combine(OutputDirectory, Path.GetFileName(BinPath)) + "\"");

            int completion = 0;

            File.Delete(Path.Combine(OutputDirectory, Path.GetFileName(BinPath)));

            if (append)
            {
                using (var oldMemoryMap = MemoryMappedFile.CreateFromFile(BinPath, FileMode.OpenOrCreate, "mappedolddata.bin"))
                {
                    using (var newMemoryMap = MemoryMappedFile.CreateFromFile(Path.Combine(OutputDirectory, Path.GetFileName(BinPath)), FileMode.OpenOrCreate, "mappednewdata.bin", Inf.DataBlockCount * InfFile.BlockSize))
                    {
                        Parallel.ForEach(Inf.Files, file =>
                        {
                            var compressedFile = new byte[file.CompressedSize];

                            if (File.Exists(Path.Combine(CompressedDirectory, file.Path)))
                            {
                                compressedFile = File.ReadAllBytes(Path.Combine(CompressedDirectory, file.Path));
                            }
                            else
                            {
                                foreach (BlockPartition partition in file.BlockPartitions)
                                {
                                    using (var accessor = oldMemoryMap.CreateViewAccessor(partition.BlockIndex * InfFile.BlockSize, InfFile.BlockSize))
                                    {
                                        accessor.ReadArray(partition.Offset, compressedFile, partition.FilePart * InfFile.BlockSize, partition.Length);
                                    }
                                }
                            }

                            foreach (BlockPartition partition in file.BlockPartitions)
                            {
                                using (var accessor = newMemoryMap.CreateViewAccessor(partition.BlockIndex * InfFile.BlockSize, InfFile.BlockSize, MemoryMappedFileAccess.Write))
                                {
                                    accessor.WriteArray(partition.Offset, compressedFile, partition.FilePart * InfFile.BlockSize, partition.Length);
                                }
                            }


                            Console.Write(++completion * 100 / Inf.Files.Count + "%\r");
                        });
                    }
                }
            }
            else
            {
                using (var memoryMap = MemoryMappedFile.CreateFromFile(Path.Combine(OutputDirectory, Path.GetFileName(BinPath)), FileMode.OpenOrCreate, "mappeddata.bin", Inf.DataBlockCount * InfFile.BlockSize))
                {
                    Parallel.ForEach(Inf.Files, file =>
                    {
                        if (File.Exists(Path.Combine(CompressedDirectory, file.Path)))
                        {
                            var compressedFile = File.ReadAllBytes(Path.Combine(CompressedDirectory, file.Path));

                            foreach (BlockPartition partition in file.BlockPartitions)
                            {
                                using (var accessor = memoryMap.CreateViewAccessor(partition.BlockIndex * InfFile.BlockSize, InfFile.BlockSize, MemoryMappedFileAccess.Write))
                                {
                                    accessor.WriteArray(partition.Offset, compressedFile, partition.FilePart * InfFile.BlockSize, partition.Length);
                                }
                            }
                        }

                        Console.Write(++completion * 100 / Inf.Files.Count + "%\r");
                    });
                }
            }
        }
    }
}
