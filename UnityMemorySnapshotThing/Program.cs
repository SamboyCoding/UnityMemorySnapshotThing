using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace UnityMemorySnapshotThing;

public static unsafe class Program
{
    public static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("No file specified");
            return;
        }
        
        var filePath = args[0];

        Console.WriteLine("Reading file...");
        using var file = new MemoryMappedFileSpanHelper<byte>(filePath);

        var magic = MemoryMarshal.Read<uint>(file[..4]);
        var endMagic = MemoryMarshal.Read<uint>(file[^4..]);
        
        Console.WriteLine($"Magic: {magic:X8}");
        Console.WriteLine($"End Magic: {endMagic:X8}");

        if(magic != MagicNumbers.HeaderMagic || endMagic != MagicNumbers.FooterMagic)
        {
            Console.WriteLine("Invalid file");
            return;
        }
    }
}