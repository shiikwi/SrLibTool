using System;
using System.IO;
using System.Linq;
using System.Text;

namespace SrLibTool
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Drag files to the program");
                return;
            }

            const string suffix = ".pck.bytes";
            foreach (var pack in args)
            {
                try
                {
                    var fileName = Path.GetFileName(pack);
                    if (!fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"Unsupport file type: {fileName}");
                        return;
                    }

                    var stem = fileName[..^suffix.Length];
                    var outDir = Path.Combine(Path.GetDirectoryName(pack)!, stem);
                    Directory.CreateDirectory(outDir);

                    var manager = new SrPackFileManager();
                    manager.RegisterPackFile(pack);
                    Unpack(manager, outDir);

                    //if (manager.GetFileNamesByPrefix("Scenario-").Any())
                    //    ExportText(manager, Path.Combine(outDir, "text"));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {pack}: {ex.Message}");
                }
            }

            Console.WriteLine("Done.");
            Console.ReadLine();
        }

        private static void Unpack(SrPackFileManager manager, string outDir)
        {
            foreach (var name in manager.GetFileNamesByPrefix(""))
            {
                var raw = manager.LoadBytes(name);

                byte[] output;
                if (IsRawMedia(raw))
                {
                    output = raw;
                }
                else
                {
                    try
                    {
                        output = SrAssetDecrypter.LoadEncryptedAsset(raw, SrAssetDecrypter.KeyString);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine($"decrypt failed, saved raw: {name}");
                        output = raw;
                    }
                }

                var dst = Path.Combine(outDir, name.Replace('/', '_'));
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.WriteAllBytes(dst, output);
                Console.WriteLine($"Export {name}");
            }
        }

        private static bool IsRawMedia(byte[] raw)
        {
            if (raw.Length < 4)
                return false;
            return raw[0] == 0x4F && raw[1] == 0x67 && raw[2] == 0x67 && raw[3] == 0x53   // OggS
                || raw[0] == 0x52 && raw[1] == 0x49 && raw[2] == 0x46 && raw[3] == 0x46;  // RIFF
        }

        private static void ExportText(SrPackFileManager manager, string textDir)
        {
            Directory.CreateDirectory(textDir);
            foreach (var name in manager.GetFileNamesByPrefix("Scenario-"))
            {
                var text = Encoding.UTF8.GetString(
                    SrAssetDecrypter.LoadEncryptedAsset(manager.LoadBytes(name), SrAssetDecrypter.KeyString));

                var dst = Path.Combine(textDir, name);
                using var writer = new StreamWriter(dst, false, new UTF8Encoding(false));
                foreach (var row in SrTsvUtility.ParseTsv(text))
                {
                    var id = row.Count > 0 ? row[0] : "";
                    var speaker = row.Count > 2 ? row[2] : "";
                    var message = row.Count > 3 ? row[3].Replace("\\n", Environment.NewLine) : "";
                    writer.WriteLine(string.IsNullOrEmpty(speaker)
                        ? $"[{id}] {message}"
                        : $"[{id}] {speaker}: {message}");
                }
                Console.WriteLine($"Parse {name}");
            }
        }
    }
}
