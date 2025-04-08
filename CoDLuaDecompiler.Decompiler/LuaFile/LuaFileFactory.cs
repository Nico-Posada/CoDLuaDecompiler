using System;
using System.IO;
using CoDLuaDecompiler.Decompiler.LuaFile.Havok;
using CoDLuaDecompiler.Decompiler.LuaFile.LuaJit;

namespace CoDLuaDecompiler.Decompiler.LuaFile
{
    public static class LuaFileFactory
    {
        public static ILuaFile Create(BinaryReader reader, string filePath, bool useDebugInfo = false)
        {
            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            var bytes = reader.ReadBytes(13);
            reader.BaseStream.Seek(0, SeekOrigin.Begin);

            // Check the .LJ magic in IW8 amd T9 lua
            if (bytes[0] == 0x1B && bytes[1] == 0x4C && bytes[2] == 0x4A)
            {
                if (bytes[3] == 2)
                    return new LuaJitFileMw(reader);
                if (bytes[3] == 0x82)
                    return new LuaJitFileBocw(reader);
                if (bytes[3] == 0x83)
                    return new LuaJitFileMw2(reader);
                if (bytes[3] == 0x84)
                    return new LuaJitFileBO6(reader);
            }
            
            if (bytes[0] != 0x1B || bytes[1] != 0x4C || bytes[2] != 0x75 || bytes[3] != 0x61)
                throw new Exception("Invalid file magic");
            
            // Check if lua version is 5.0
            if(bytes[4] == 0x50)
                throw new NotImplementedException("5.0 lua isn't implemented");
            
            // Check compiler version
            if(bytes[5] == 0x0D && bytes[12] == 0x00)
                return new HavokLuaFileT6(reader);

            if (bytes[12] == 0x03)
                return new HavokLuaFileIW(reader);

            if (useDebugInfo)
            {
                var debugFile = Path.ChangeExtension(filePath, ".lua");
                using var stream = File.OpenRead(debugFile);
                using var debugReader = new BinaryReader(stream);
                return new HavokLuaFileT7(reader, debugReader);
            }
            else
                return new HavokLuaFileT7(reader);
        }

        public static ILuaFile Create(string filePath, bool usesDebugInfo = false)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File {filePath} not found!", filePath);
            }

            using var stream = File.OpenRead(filePath);
            using var reader = new BinaryReader(stream);

            return Create(reader, filePath, usesDebugInfo);
        }
    }
}