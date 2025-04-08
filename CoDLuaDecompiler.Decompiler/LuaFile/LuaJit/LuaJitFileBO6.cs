using System.IO;
using CoDLuaDecompiler.Decompiler.LuaFile.Structures.LuaFunction.LuaJit;

namespace CoDLuaDecompiler.Decompiler.LuaFile.LuaJit
{
    public class LuaJitFileBO6 : LuaJitFile
    {
        public LuaJitFileBO6(BinaryReader reader) : base(reader)
        {
        }

        protected override LuaJitFunction ReadFunction()
        {
            return new LuaJitFunctionBO6(this, Reader);
        }
    }
}