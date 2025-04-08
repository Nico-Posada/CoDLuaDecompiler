using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CoDLuaDecompiler.Common;
using CoDLuaDecompiler.Decompiler.Extensions;
using CoDLuaDecompiler.Decompiler.IR.Functions;
using CoDLuaDecompiler.Decompiler.LuaFile.LuaJit;
using CoDLuaDecompiler.Decompiler.LuaFile.Structures.FileHeader;
using CoDLuaDecompiler.Decompiler.LuaFile.Structures.LuaConstant;
using CoDLuaDecompiler.Decompiler.LuaFile.Structures.LuaConstant.LuaJit;
using CoDLuaDecompiler.Decompiler.LuaFile.Structures.LuaFunction.Structures;
using CoDLuaDecompiler.Decompiler.LuaFile.Structures.LuaFunction.Structures.LuaJit;
using CoDLuaDecompiler.Decompiler.LuaFile.Structures.LuaOpCodeTable.LuaJit;

namespace CoDLuaDecompiler.Decompiler.LuaFile.Structures.LuaFunction.LuaJit
{
    public class LuaJitFunction : LuaFunction
    {
        public virtual ILuaJitOpCodeTable OpCodeTable => new LuaJitOpCodeTable();
        public List<ILuaJitConstant> UpvalueReferences { get; private set; }
        public List<ILuaJitConstant> Constants { get; protected set; }
        
        public LuaJitFunction(ILuaFile luaFile, BinaryReader reader) : base(luaFile, reader)
        {
        }

        public override Function IRFunction { get; set; }

        protected override FunctionHeader ReadFunctionHeader()
        {
            var functionLength = Reader.ReadULEB128();
            if (functionLength == 0)
            {
                FunctionPos = -1;
                return null;
            }
            var header = new LuaJitFunctionHeader();

            var flags = Reader.ReadByte();
            header.HasFfi = Convert.ToBoolean(flags & 0b_0000_0100);
            header.HasILoop = Convert.ToBoolean(flags & 0b_0001_0000);
            header.JitDisabled = Convert.ToBoolean(flags & 0b_0000_1000);
            header.HasChild = Convert.ToBoolean(flags & 0b_0000_0001);
            header.UsesVarArg = Convert.ToBoolean(flags & 0b_0000_0010);

            header.ParameterCount = Reader.ReadByte();
            header.RegisterCount = Reader.ReadByte();
            header.UpvaluesCount = Reader.ReadByte();
            header.ComplexConstantsCount = Reader.ReadULEB128();
            header.NumericConstantsCount = Reader.ReadULEB128();
            header.InstructionCount = (int) Reader.ReadULEB128();

            if (!((LuaJitFileHeader)LuaFile.Header).IsStripped)
            {
                header.DebugInfoSize = Reader.ReadULEB128();
                header.FirstLineNumber = Reader.ReadULEB128();
                header.LinesCount = Reader.ReadULEB128();
            }

            return header;
        }

        protected override Instruction ReadInstruction()
        {
            var instr = new LuaJitInstruction();

            var codeword = Reader.ReadUInt32();

            instr.OpCode = LuaJitOpCode.UNKNW;
            if (OpCodeTable.TryGetValue((int) (codeword & 0xFF), out var opCode)) instr.OpCode = opCode;

            dynamic ProcessOperand(LuaJitArgumentType type, dynamic operand)
            {
                if (type is LuaJitArgumentType.Str or LuaJitArgumentType.Tab or LuaJitArgumentType.Fun or LuaJitArgumentType.Cdt)
                    return ((long) ((LuaJitFunctionHeader)Header).ComplexConstantsCount) - operand - 1;

                if (type == LuaJitArgumentType.Jmp) return operand - 0x8000;
                
                if (type == LuaJitArgumentType.Slit || type == LuaJitArgumentType.Num) return BitConverter.ToInt16(BitConverter.GetBytes(operand));

                return operand;
            }

            var a = (codeword >> 8) & 0xFF;
            uint b = 0;
            long cd;
            if (instr.OpCode.ArgumentsCount == 3)
            {
                cd = (codeword >> 16) & 0xFF;
                b = (codeword >> 24) & 0xFF;
            }
            else
            {
                cd = (codeword >> 16) & 0xFFFF;
            }

            if (instr.OpCode.AType != LuaJitArgumentType.None)
                instr.A = ProcessOperand(instr.OpCode.AType, a);
            if (instr.OpCode.BType != LuaJitArgumentType.None)
                instr.B = ProcessOperand(instr.OpCode.BType, b);
            if (instr.OpCode.CDType != LuaJitArgumentType.None)
                instr.CD = ProcessOperand(instr.OpCode.CDType, cd);

            return instr;
        }

        protected override void ReadConstants()
        {
            UpvalueReferences = new List<ILuaJitConstant>();
            var constants = new List<ILuaJitConstant>();
            // Read the upvalue references
            for (var i = 0; i < Header.UpvaluesCount; i++) UpvalueReferences.Add(ReadUpvalueReference());

            // Read the complex constants
            for (ulong i = 0; i < ((LuaJitFunctionHeader)Header).ComplexConstantsCount; i++) constants.Add(ReadComplexConstant());

            // Read the numeric constants
            for (ulong i = 0; i < ((LuaJitFunctionHeader)Header).NumericConstantsCount; i++) constants.Add(ReadNumericConstant());
            
            ReadDebugInfo();

            Constants = constants;
        }
        
        protected virtual ILuaJitConstant ReadUpvalueReference()
        {
            var index = Reader.ReadByte();
            var idfk = Reader.ReadByte();
            //if (idfk != 128)
            //    throw new Exception("WHAT");
            return new LuaJitConstant(index);
        }
        
        protected virtual ILuaJitConstant ReadComplexConstant()
        {
            var type = Reader.ReadULEB128();

            if (type >= 5)
            {
                var str = Encoding.UTF8.GetString(Reader.ReadBytes((int) (type - 5)));
                
                if (!String.IsNullOrEmpty(str) && str.Length > 10 && str.StartsWith("x64:"))
                {
                    var hash = Convert.ToUInt64(str.Substring(4).Replace(".lua", ""), 16) & AppInfo.HashMask;
                    if (Decompiler.HashEntries.ContainsKey(hash))
                        str = Decompiler.HashEntries[hash];
                    else
                        // Replace the hash with the masked version so it's easier to find the actual file back
                        str = $"x64:{hash:x}";
                }

                return new LuaJitConstant(str);
            }

            if (type == 0)
            {
                var child = ((LuaJitFile)LuaFile).Functions.Pop();
                ChildFunctions.Add(child);
                return new LuaJitConstant((LuaJitFunction) child);
            }

            if (type == 1)
                // Parse table
                return new LuaJitConstant(ReadTable());
            if (type == 2) Console.WriteLine("TYPE 2");
            if (type == 3) Console.WriteLine("TYPE 3");
            if (type == 4) Console.WriteLine("TYPE 4");
            Console.WriteLine("unknown TYPE " + type);

            return null;
        }

        protected virtual LuaJitTable ReadTable()
        {
            var table = new LuaJitTable();
            var arrayItemsCount = Reader.ReadULEB128();
            var hashItemsCount = Reader.ReadULEB128();

            for (ulong i = 0; i < arrayItemsCount; i++) table.Array.Add(ReadTableItem());

            for (ulong i = 0; i < hashItemsCount; i++)
            {
                var key = ReadTableItem();
                var value = ReadTableItem();

                table.Dictionary.Add(key, value);
            }

            return table;
        }

        protected virtual double ReadNumber()
        {
            var hi = Reader.ReadULEB128();
            var lo = Reader.ReadULEB128();

            return AssembleNumber(lo, hi);
        }

        protected virtual double AssembleNumber(ulong low, ulong high)
        {
            ulong floatAsInt = high << 32 | low;
            return BitConverter.ToDouble(BitConverter.GetBytes(floatAsInt));
        }

        protected virtual ILuaJitConstant ReadNumericConstant()
        {
            var isnumLo = Reader.ReadULEB128_from33bit();

            ILuaJitConstant luaJitConstant;
            if (isnumLo.Item1)
            {
                var hi = Reader.ReadULEB128();

                luaJitConstant = new LuaJitConstant(AssembleNumber(isnumLo.Item2, hi));
            }
            else
            {
                var number = isnumLo.Item2;
                if ((number & 0x80000000) != 0)
                    luaJitConstant = new LuaJitConstant((double) -0x100000000 + number);
                else
                    luaJitConstant = new LuaJitConstant((double) number);
            }

            return luaJitConstant;
        }

        protected virtual ILuaJitConstant ReadTableItem()
        {
            var type = Reader.ReadULEB128();

            if (type > 5)
                return new LuaJitConstant(Encoding.UTF8.GetString(Reader.ReadBytes((int) (type - 5))));
            if (type == 3)
                return new LuaJitConstant(Reader.ReadLEB128());
            if (type == 4)
                return new LuaJitConstant(ReadNumber());
            if (type == 2)
                return new LuaJitConstant(true);
            if (type == 1)
                return new LuaJitConstant(false);
            return new LuaJitConstant();
        }

        protected virtual void ReadDebugInfo()
        {
            Reader.ReadBytes((int) ((LuaJitFunctionHeader)Header).DebugInfoSize);
        }
    }
}