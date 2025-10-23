using System;
using System.Text;
using System.IO;

namespace Kurumi
{
    class Program
    {
        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            if (args.Length < 3)
            {
                ShowUsage();
                return;
            }

            string mode = args[0].ToLower();
            string inputPath = args[1];
            string outputPath = args[2];
            
            // Parse compression flags (optional parameters)
            bool compressFiles = true;  // Default: compress files
            bool compressIndex = false; // Default: don't compress index
            
            if (args.Length > 3)
            {
                // Parse file compression flag
                if (bool.TryParse(args[3], out bool fileCompression))
                {
                    compressFiles = fileCompression;
                }
                else
                {
                    Console.WriteLine($"Warning: Invalid compression flag '{args[3]}'. Using default (true).");
                }
            }
            
            if (args.Length > 4)
            {
                // Parse index compression flag
                if (bool.TryParse(args[4], out bool indexCompression))
                {
                    compressIndex = indexCompression;
                }
                else
                {
                    Console.WriteLine($"Warning: Invalid index compression flag '{args[4]}'. Using default (false).");
                }
            }

            try
            {
                var kurumi = new Kurumi();

                switch (mode)
                {
                    case "unpack":
                        // Validate input file exists
                        if (!File.Exists(inputPath))
                        {
                            Console.WriteLine($"Error: Input file '{inputPath}' does not exist.");
                            return;
                        }

                        // Create output directory if it doesn't exist
                        Directory.CreateDirectory(outputPath);

                        Console.WriteLine($"Extracting '{inputPath}' to '{outputPath}'...");

                        DateTime startTime = DateTime.Now;
                        kurumi.Unpack(inputPath, outputPath);

                        TimeSpan unpackDuration = DateTime.Now - startTime;
                        int unpackFileCount = Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories).Length;

                        Console.WriteLine("Extraction completed successfully!");
                        Console.WriteLine($"Extracted {unpackFileCount} files");
                        Console.WriteLine($"Extraction time: {unpackDuration.TotalSeconds:F2} seconds");
                        break;

                    case "pack":
                        // Validate input folder exists
                        if (!Directory.Exists(inputPath))
                        {
                            Console.WriteLine($"Error: Input folder '{inputPath}' does not exist.");
                            return;
                        }

                        Console.WriteLine($"Packing '{inputPath}' into '{outputPath}'...");
                        Console.WriteLine($"File compression: {(compressFiles ? "enabled" : "disabled")}");
                        Console.WriteLine($"Index compression: {(compressIndex ? "enabled" : "disabled")}");

                        DateTime packStartTime = DateTime.Now;
                        kurumi.Pack(inputPath, outputPath, compressFiles, compressIndex);

                        TimeSpan packDuration = DateTime.Now - packStartTime;
                        FileInfo packInfo = new FileInfo(outputPath);

                        Console.WriteLine("Packing completed successfully!");
                        Console.WriteLine($"Archive created: {outputPath}");
                        Console.WriteLine($"Archive size: {packInfo.Length:N0} bytes");
                        Console.WriteLine($"Packing time: {packDuration.TotalSeconds:F2} seconds");
                        break;

                    default:
                        Console.WriteLine($"Error: Unknown mode '{mode}'.");
                        ShowUsage();
                        break;
                }
            }
            catch (InvalidDataException ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine("This may indicate an unsupported or corrupted file format.");
            }
            catch (DirectoryNotFoundException ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Error: Access denied - {ex.Message}");
            }
            catch (IOException ex)
            {
                Console.WriteLine($"I/O Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static void ShowUsage()
        {
            Console.WriteLine("Kurumi File Tool");
            Console.WriteLine("Usage: Kurumi.exe <mode> <input_path> <output_path> [compress_files] [compress_index]");
            Console.WriteLine("  <mode>           Mode of operation: 'unpack' or 'pack'");
            Console.WriteLine("  <input_path>     Path to the input file or folder");
            Console.WriteLine("  <output_path>    Destination path for output file or folder");
            Console.WriteLine("  [compress_files] Optional: true/false - compress individual files (default: true)");
            Console.WriteLine("  [compress_index] Optional: true/false - compress file index (default: false)");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  Kurumi.exe unpack game_data.mpk extracted_files");
            Console.WriteLine("  Kurumi.exe pack extracted_files packed_data.mpk");
            Console.WriteLine("  Kurumi.exe pack extracted_files packed_data.mpk true false");
            Console.WriteLine("  Kurumi.exe pack extracted_files packed_data.mpk false false   # No compression");
            Console.WriteLine("  Kurumi.exe pack extracted_files packed_data.mpk true true    # Full compression");
        }
    }
}