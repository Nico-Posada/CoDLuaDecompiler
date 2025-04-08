using CoDLuaDecompiler.Decompiler.LuaFile.Structures.LuaFunction;
using CoDLuaDecompiler.Decompiler.LuaFile.Structures.LuaFunction.LuaJit;
using CoDLuaDecompiler.Decompiler.LuaFile.Structures.LuaFunction.Structures.LuaJit;

namespace CoDLuaDecompiler.Decompiler.LuaFile.Structures.LuaConstant.LuaJit
{
    public abstract class ILuaJitConstant
    {
        public LuaJitConstantType Type { get; protected set; }
        public string StringValue { get; protected set; }
        public double NumberValue { get; protected set; }
        public LuaJitFunction Function { get; set; }
        public LuaJitTable Table { get; set; }
        public bool BoolValue { get; protected set; }
        public ulong HashValue { get; protected set; }
        public int? HashType { get; protected set; }
    }
}