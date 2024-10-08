using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace HaSharedLibrary.Util
{
    public class AssemblyBitnessDetector
    {
        public enum Bitness
        {
            Bit32,
            Bit64,
            AnyCPU
        }

        public static Bitness GetAssemblyBitness()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyPath = assembly.Location;

            try
            {
                using var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(stream);

                // Read DOS header
                stream.Seek(0x3C, SeekOrigin.Begin);
                int peHeaderOffset = reader.ReadInt32();

                // Read PE header signature
                stream.Seek(peHeaderOffset, SeekOrigin.Begin);
                uint peHeaderSignature = reader.ReadUInt32();
                if (peHeaderSignature != 0x00004550) // "PE\0\0"
                {
                    throw new InvalidOperationException("Invalid PE header signature.");
                }

                // Read PE file header
                stream.Seek(peHeaderOffset + 4 + 2, SeekOrigin.Begin);
                ushort machine = reader.ReadUInt16();

                switch (machine)
                {
                    case 0x014c: // IMAGE_FILE_MACHINE_I386
                        return Bitness.Bit32;
                    case 0x8664: // IMAGE_FILE_MACHINE_AMD64
                        return Bitness.Bit64;
                    case 0x0200: // IMAGE_FILE_MACHINE_IA64
                        return Bitness.Bit64;
                    default:
                        // If it's not explicitly 32-bit or 64-bit, assume it's AnyCPU
                        return Bitness.AnyCPU;
                }
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as appropriate for your application
                Console.WriteLine($"Error detecting assembly bitness: {ex.Message}");
                return Bitness.AnyCPU; // Default to AnyCPU if detection fails
            }
        }
    }
}
