using System;
using System.IO;

namespace InfBinRepackager
{
    public class Program
    {
        public static string ExportSinglePath = string.Empty;
        public static string InfPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "input", "data.inf");
        public static string BinPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "input", "data.bin");
        public static string OutputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
        public static int Seed = 0x1C75BD9F;
        public static int CompressionLevel = 0;

        static void Main(string[] args)
        {
            bool invalid = false;
            bool help = false;
            bool print = false;
            bool keepCompressed = false;
            bool import = false;
            bool create = false;
            bool export = false;
            bool exportSingle = false;

            try
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i].ToLower() == "-h" || args[i].ToLower() == "--help")
                    {
                        help = true;
                        break;
                    }
                    else if (args[i].ToLower() == "-e" || args[i].ToLower() == "--export")
                    {
                        export = true;
                    }
                    else if ((args[i].ToLower() == "-es" || args[i].ToLower() == "--export-single") && i < args.Length - 1)
                    {
                        exportSingle = true;
                        ExportSinglePath = args[++i];
                    }
                    else if (args[i].ToLower() == "-i" || args[i].ToLower() == "--import")
                    {
                        import = true;
                    }
                    else if (args[i].ToLower() == "-c" || args[i].ToLower() == "--create")
                    {
                        create = true;
                    }
                    else if (args[i].ToLower() == "-p" || args[i].ToLower() == "--print")
                    {
                        print = true;
                    }
                    else if ((args[i].ToLower() == "-if" || args[i].ToLower() == "--inf-file") && i < args.Length - 1)
                    {
                        InfPath = args[++i];
                    }
                    else if ((args[i].ToLower() == "-bf" || args[i].ToLower() == "--bin-file") && i < args.Length - 1)
                    {
                        BinPath = args[++i];
                    }
                    else if ((args[i].ToLower() == "-od" || args[i].ToLower() == "--output-directory") && i < args.Length - 1)
                    {
                        OutputDirectory = args[++i];
                    }
                    else if ((args[i].ToLower() == "-cl" || args[i].ToLower() == "--compression-level") && i < args.Length - 1)
                    {
                        CompressionLevel = Convert.ToInt32(args[++i]);
                    }
                    else if (args[i].ToLower() == "-kc" || args[i].ToLower() == "--keep-compressed")
                    {
                        keepCompressed = true;
                    }
                    else if ((args[i].ToLower() == "-s" || args[i].ToLower() == "--seed") && i < args.Length - 1)
                    {
                        Seed = Convert.ToInt32(args[++i]);
                    }
                    else
                    {
                        invalid = true;
                        Console.WriteLine("Invalid argument: " + args[i]);
                        break;
                    }
                }

                if (help || args.Length == 0)
                {
                    DisplayHelp();
                }
                else if(!invalid)
                {
                    Repacker repacker = new Repacker(Seed, InfPath, BinPath, OutputDirectory, !create);

                    if (export)
                        repacker.Export(keepCompressed);

                    if (exportSingle)
                        repacker.Export(keepCompressed, ExportSinglePath);

                    if (import)
                        repacker.Import(true, keepCompressed, CompressionLevel);

                    if (create)
                        repacker.Import(false, keepCompressed, CompressionLevel);

                    if (print)
                        repacker.Print();
                }
                else
                {
                    Console.WriteLine("-h/--help to display help");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + "\r\n" + e.StackTrace);
            }
        }

        public static void DisplayHelp()
        {
            Console.WriteLine("Description:");
            Console.WriteLine("  Import and export files of a \".inf\"/\".bin\" file system");
            Console.WriteLine("");
            Console.WriteLine("Usage:");
            Console.WriteLine("  " + AppDomain.CurrentDomain.FriendlyName + " [commands] [options]");
            Console.WriteLine("");
            Console.WriteLine("Commands:");
            Console.WriteLine("  -e /--export                   Export all files from the file system");
            Console.WriteLine("  -es/--export-single     [arg]  Export a single file from the file system");
            Console.WriteLine("  -i /--import                   Import files from the output directory");
            Console.WriteLine("  -c /--create                   Create a new \".inf\"/\".bin\" file system from the files in the output directory");
            Console.WriteLine("  -p /--print                    Print the content of the \".inf\" file");
            Console.WriteLine("");
            Console.WriteLine("Options:");
            Console.WriteLine("  -if/--inf-file          [arg]  Path to the \".inf\" file - Default: " + InfPath);
            Console.WriteLine("  -bf/--bin-file          [arg]  Path to the \".bin\" file - Default: " + BinPath);
            Console.WriteLine("  -od/--output-directory  [arg]  Output directory - Default: " + OutputDirectory);
            Console.WriteLine("  -cl/--compression-level [arg]  LZ4 compression level of the files between 0 and 12 - Default: " + CompressionLevel);
            Console.WriteLine("  -kc/--keep-compressed          Skip the compression when importing and the decompression when exporting");
            Console.WriteLine("  -s /--seed              [arg]  32bits xxHash seed used to calculate checksums - Default: " + Seed);
            Console.WriteLine("  -h /--help                     Display help");
        }
    }
}
