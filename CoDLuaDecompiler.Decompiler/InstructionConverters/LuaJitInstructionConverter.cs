using System;
using System.Collections.Generic;
using System.Linq;
using CoDLuaDecompiler.Decompiler.IR;
using CoDLuaDecompiler.Decompiler.IR.Expression;
using CoDLuaDecompiler.Decompiler.IR.Expression.Operator;
using CoDLuaDecompiler.Decompiler.IR.Functions;
using CoDLuaDecompiler.Decompiler.IR.Identifiers;
using CoDLuaDecompiler.Decompiler.IR.Instruction;
using CoDLuaDecompiler.Decompiler.LuaFile.Structures.LuaConstant.LuaJit;
using CoDLuaDecompiler.Decompiler.LuaFile.Structures.LuaFunction;
using CoDLuaDecompiler.Decompiler.LuaFile.Structures.LuaFunction.LuaJit;
using CoDLuaDecompiler.Decompiler.LuaFile.Structures.LuaFunction.Structures;
using CoDLuaDecompiler.Decompiler.LuaFile.Structures.LuaFunction.Structures.LuaJit;
using CoDLuaDecompiler.Decompiler.LuaFile.Structures.LuaOpCodeTable.LuaJit;
using ValueType = CoDLuaDecompiler.Decompiler.IR.Identifiers.ValueType;

namespace CoDLuaDecompiler.Decompiler.InstructionConverters
{
    public class LuaJitInstructionConverter : IInstructionConverter
    {
        private SymbolTable _symbolTable;
        
        private IdentifierReference RegisterReference(long regIndex)
        {
            return new IdentifierReference(_symbolTable.GetRegister((uint) regIndex));
        }

        private ILuaJitConstant GetNumConstant(LuaJitFunction luaFunc, ulong index)
        {
            return luaFunc.Constants[(int) (((LuaJitFunctionHeader) luaFunc.Header).ComplexConstantsCount + index)];
        }

        private IExpression ConvertLuaJitConstant(ILuaJitConstant con, int id)
        {
            if (con.Type == LuaJitConstantType.Number)
                return new Constant(con.NumberValue, id);
            if (con.Type == LuaJitConstantType.String)
                return new Constant(con.StringValue, id);
            if (con.Type == LuaJitConstantType.Boolean)
                return new Constant(con.BoolValue, id);
            if (con.Type == LuaJitConstantType.Hash)
                return new Constant(con.HashValue, con.HashType, id);
            if (con.Type == LuaJitConstantType.Function)
            {
                foreach (var upvalueReference in con.Function.UpvalueReferences)
                {
                    con.Function.IRFunction.UpvalueBindings.Add(_symbolTable.GetRegister((uint) upvalueReference.NumberValue));
                }
                return new Closure(con.Function.IRFunction);
            }
            if (con.Type == LuaJitConstantType.Table)
            {
                var il = new InitializerList();
                
                foreach (var value in con.Table.Array)
                    il.Expressions.Add(ConvertLuaJitConstant(value, -1));

                foreach (var dictonary in con.Table.Dictionary)
                    il.Expressions.Add(new ListAssignment(ConvertLuaJitConstant(dictonary.Key, -1), ConvertLuaJitConstant(dictonary.Value, -1)));

                return il;
            }
            return new Constant(ValueType.Nil, id);
        }

        private Constant GetPrimitiveConstant(long value)
        {
            if (value == 0)
                return new Constant(ValueType.Nil, -1);
            return new Constant(value == 2, -1);
        }
        
        public void Convert(Function function, ILuaFunction luaFunc)
        {
            _symbolTable = function.SymbolTable;
            var luaFunction = (LuaJitFunction) luaFunc;
            
            // Loop over all instructions
            for (int pos = 0; pos < luaFunction.Instructions.Count; pos++)
            {
                var i = (LuaJitInstruction) luaFunction.Instructions[pos];
                
                List<IInstruction> instrs = new List<IInstruction>();
                switch (i.OpCode.Name)
                {
                    // Comparison ops
                    case "ISLT":
                        instrs.Add(new Jump(
                            function.GetLabel((uint)(pos + 2)), 
                            new BinOp(RegisterReference(i.A), RegisterReference(i.CD), BinOperationType.OpLessThan)
                            ));
                        break;
                    case "ISGE":
                        instrs.Add(new Jump(
                            function.GetLabel((uint)(pos + 2)), 
                            new BinOp(RegisterReference(i.A), RegisterReference(i.CD), BinOperationType.OpGreaterEqual)
                        ));
                        break;
                    case "ISLE":
                        instrs.Add(new Jump(
                            function.GetLabel((uint)(pos + 2)), 
                            new BinOp(RegisterReference(i.A), RegisterReference(i.CD), BinOperationType.OpLessEqual)
                        ));
                        break;
                    case "ISGT":
                        instrs.Add(new Jump(
                            function.GetLabel((uint)(pos + 2)), 
                            new BinOp(RegisterReference(i.A), RegisterReference(i.CD), BinOperationType.OpGreaterThan)
                        ));
                        break;
                    case "ISEQV":
                        instrs.Add(new Jump(
                            function.GetLabel((uint)(pos + 2)), 
                            new BinOp(RegisterReference(i.A), RegisterReference(i.CD), BinOperationType.OpEqual)
                        ));
                        break;
                    case "ISNEV":
                        instrs.Add(new Jump(
                            function.GetLabel((uint)(pos + 2)), 
                            new BinOp(RegisterReference(i.A), RegisterReference(i.CD), BinOperationType.OpNotEqual)
                        ));
                        break;
                    case "ISEQS":
                        instrs.Add(new Jump(
                            function.GetLabel((uint)(pos + 2)), 
                            new BinOp(RegisterReference(i.A), ConvertLuaJitConstant(luaFunction.Constants[(int) i.CD], (int) i.CD), BinOperationType.OpEqual)
                        ));
                        break;
                    case "ISNES":
                        instrs.Add(new Jump(
                            function.GetLabel((uint)(pos + 2)), 
                            new BinOp(RegisterReference(i.A), ConvertLuaJitConstant(luaFunction.Constants[(int) i.CD], (int) i.CD), BinOperationType.OpNotEqual)
                        ));
                        break;
                    case "ISEQN":
                        instrs.Add(new Jump(
                            function.GetLabel((uint)(pos + 2)), 
                            new BinOp(RegisterReference(i.A), ConvertLuaJitConstant(GetNumConstant(luaFunction, (ulong) i.CD), -1), BinOperationType.OpEqual)
                        ));
                        break;
                    case "ISNEN":
                        instrs.Add(new Jump(
                            function.GetLabel((uint)(pos + 2)), 
                            new BinOp(RegisterReference(i.A), ConvertLuaJitConstant(GetNumConstant(luaFunction, (ulong) i.CD), -1), BinOperationType.OpNotEqual)
                        ));
                        break;
                    case "ISEQP":
                        instrs.Add(new Jump(
                            function.GetLabel((uint)(pos + 2)), 
                            new BinOp(RegisterReference(i.A), GetPrimitiveConstant(i.CD), BinOperationType.OpEqual)
                        ));
                        break;
                    case "ISNEP":
                        instrs.Add(new Jump(
                            function.GetLabel((uint)(pos + 2)), 
                            new BinOp(RegisterReference(i.A), GetPrimitiveConstant(i.CD), BinOperationType.OpNotEqual)
                        ));
                        break;
                    
                    // Unary test and copy ops
                    case "ISTC":
                        instrs.Add(new Jump(
                            function.GetLabel((uint) (pos + 2)),
                            new UnaryOp(RegisterReference((uint) i.CD), UnOperationType.OpNot)));
                        instrs.Add(new Assignment(_symbolTable.GetRegister(i.A), new IdentifierReference(_symbolTable.GetRegister((uint) i.CD))));
                        break;
                    case "ISFC":
                        instrs.Add(new Jump(
                            function.GetLabel((uint) (pos + 2)),
                            RegisterReference((uint) i.CD)));
                        instrs.Add(new Assignment(_symbolTable.GetRegister(i.A), new IdentifierReference(_symbolTable.GetRegister((uint) i.CD))));
                        break;
                    case "IST":
                        instrs.Add(new Jump(function.GetLabel((uint) (pos + 2)),
                            RegisterReference(i.CD)));
                        break;
                    case "ISF":
                        instrs.Add(new Jump(function.GetLabel((uint) (pos + 2)),
                            new UnaryOp(RegisterReference(i.CD), UnOperationType.OpNot)));
                        break;
                    
                    // Unary ops
                    case "MOV":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            RegisterReference(i.CD)));
                        break;
                    case "NOT":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new UnaryOp(new IdentifierReference(_symbolTable.GetRegister((uint) i.CD)), UnOperationType.OpNot)
                        ));
                        break;
                    case "UNM":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new UnaryOp(new IdentifierReference(_symbolTable.GetRegister((uint) i.CD)), UnOperationType.OpNegate)
                        ));
                        break;
                    case "LEN":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new UnaryOp(new IdentifierReference(_symbolTable.GetRegister((uint) i.CD)), UnOperationType.OpLength)
                        ));
                        break;
                    
                    // Binary ops
                    case "ADDVN":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new BinOp(RegisterReference(i.B), ConvertLuaJitConstant(GetNumConstant(luaFunction, (ulong) i.CD), -1), BinOperationType.OpAdd)));
                        break;
                    case "SUBVN":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new BinOp(RegisterReference(i.B), ConvertLuaJitConstant(GetNumConstant(luaFunction, (ulong) i.CD), -1), BinOperationType.OpSub)));
                        break;
                    case "MULVN":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new BinOp(RegisterReference(i.B), ConvertLuaJitConstant(GetNumConstant(luaFunction, (ulong) i.CD), -1), BinOperationType.OpMul)));
                        break;
                    case "DIVVN":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new BinOp(RegisterReference(i.B), ConvertLuaJitConstant(GetNumConstant(luaFunction, (ulong) i.CD), -1), BinOperationType.OpDiv)));
                        break;
                    case "MODVN":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new BinOp(RegisterReference(i.B), ConvertLuaJitConstant(GetNumConstant(luaFunction, (ulong) i.CD), -1), BinOperationType.OpMod)));
                        break;
                    case "ADDNV":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new BinOp(ConvertLuaJitConstant(GetNumConstant(luaFunction, (ulong) i.CD), -1), RegisterReference(i.B), BinOperationType.OpAdd)));
                        break;
                    case "SUBNV":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new BinOp(ConvertLuaJitConstant(GetNumConstant(luaFunction, (ulong) i.CD), -1), RegisterReference(i.B), BinOperationType.OpSub)));
                        break;
                    case "MULNV":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new BinOp(ConvertLuaJitConstant(GetNumConstant(luaFunction, (ulong) i.CD), -1), RegisterReference(i.B), BinOperationType.OpMul)));
                        break;
                    case "DIVNV":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new BinOp(ConvertLuaJitConstant(GetNumConstant(luaFunction, (ulong) i.CD), -1), RegisterReference(i.B), BinOperationType.OpDiv)));
                        break;
                    case "MODNV":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new BinOp(ConvertLuaJitConstant(GetNumConstant(luaFunction, (ulong) i.CD), -1), RegisterReference(i.B), BinOperationType.OpMod)));
                        break;
                    case "ADDVV":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new BinOp(RegisterReference(i.B), RegisterReference(i.CD), BinOperationType.OpAdd)));
                        break;
                    case "SUBVV":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new BinOp(RegisterReference(i.B), RegisterReference(i.CD), BinOperationType.OpSub)));
                        break;
                    case "MULVV":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new BinOp(RegisterReference(i.B), RegisterReference(i.CD), BinOperationType.OpMul)));
                        break;
                    case "DIVVV":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new BinOp(RegisterReference(i.B), RegisterReference(i.CD), BinOperationType.OpDiv)));
                        break;
                    case "MODVV":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new BinOp(RegisterReference(i.B), RegisterReference(i.CD), BinOperationType.OpMod)));
                        break;
                    case "POW":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new BinOp(RegisterReference(i.B), RegisterReference(i.CD), BinOperationType.OpPow)));
                        break;
                    case "CAT":
                        var args = new List<IExpression>();
                        for (int arg = (int) i.B; arg <= i.CD; arg++)
                            args.Add(new IdentifierReference(_symbolTable.GetRegister((uint)arg)));
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new Concat(args)));
                        break;
                    
                    // Constant ops
                    case "KSTR":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            ConvertLuaJitConstant(luaFunction.Constants[(int) i.CD], (int) i.CD)));
                        break;
                    case "KHASH":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            ConvertLuaJitConstant(luaFunction.Constants[(int) i.CD], (int) i.CD)));
                        break;
                    case "KSHORT":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new Constant( (int) i.CD, -1)));
                        break;
                    case "KNUM":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            ConvertLuaJitConstant(GetNumConstant(luaFunction, (ulong) i.CD), -1)));
                        break;
                    case "KPRI":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            GetPrimitiveConstant(i.CD)
                            ));
                        break;
                    case "KNIL":
                        var nlist = new List<IdentifierReference>();
                        for (int arg = (int) i.A; arg <= i.CD; arg++)
                            nlist.Add(new IdentifierReference(_symbolTable.GetRegister((uint) arg)));
                        instrs.Add(new Assignment(nlist, new Constant(ValueType.Nil, -1)));
                        break;
                    
                    // Upvalue and function ops
                    case "UGET":
                        Identifier up = function.UpvalueBindings[(int) i.CD];
                        up.IsClosureBound = true;
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new IdentifierReference(up)));
                        break;
                    case "USETV":
                        instrs.Add(new Assignment(
                            _symbolTable.GetUpValue(i.A, function.UpvalueBindings[(int) i.A].Name),
                            new IdentifierReference(_symbolTable.GetRegister((uint) i.CD))));
                        break;
                    case "USETS":
                        instrs.Add(new Assignment(
                            _symbolTable.GetUpValue(i.A, function.UpvalueBindings[(int) i.A].Name),
                            ConvertLuaJitConstant(luaFunction.Constants[(int) i.CD], (int) i.CD)));
                        break;
                    case "USETN":
                        instrs.Add(new Assignment(
                            _symbolTable.GetUpValue(i.A, function.UpvalueBindings[(int) i.A].Name),
                            ConvertLuaJitConstant(GetNumConstant(luaFunction, (ulong) i.CD), -1)));
                        break;
                    case "USETP":
                        instrs.Add(new Assignment(
                            _symbolTable.GetUpValue(i.A, function.UpvalueBindings[(int) i.A].Name),
                            GetPrimitiveConstant(i.CD)));
                        break;
                    case "UCLO":
                        // This doesn't seem to do anything
                        instrs.Add(new Data(i));
                        break;
                    case "FNEW":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            ConvertLuaJitConstant(luaFunction.Constants[(int) i.CD], (int) i.CD)
                        ));
                        break;
                    
                    // Table ops
                    case "TNEW":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new InitializerList()
                            ));
                        break;
                    case "TDUP":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            ConvertLuaJitConstant(luaFunction.Constants[(int) i.CD], (int) i.CD)
                        ));
                        break;
                    case "GGET":
                        instrs.Add(new Assignment(
                            _symbolTable.GetRegister(i.A),
                            new IdentifierReference(_symbolTable.GetGlobal(luaFunction.Constants[(int) i.CD].ToString(), (int) i.A))
                        ));
                        break;
                    case "GSET":
                        instrs.Add(new Assignment(
                            _symbolTable.GetGlobal(luaFunction.Constants[(int) i.CD].ToString(), (int)i.CD),
                            new IdentifierReference(_symbolTable.GetRegister(i.A))));
                        break;
                    case "TGETV":
                        instrs.Add(new Assignment(
                            RegisterReference(i.A),
                            new IdentifierReference(_symbolTable.GetRegister(i.B), RegisterReference(i.CD))
                        ));
                        break;
                    case "TGETS":
                        instrs.Add(new Assignment(
                            RegisterReference(i.A),
                            new IdentifierReference(_symbolTable.GetRegister(i.B), ConvertLuaJitConstant(luaFunction.Constants[(int) i.CD], (int) i.CD))
                            ));
                        break;
                    case "TGETB":
                        instrs.Add(new Assignment(
                            RegisterReference(i.A),
                            new IdentifierReference(_symbolTable.GetRegister(i.B), new Constant((int) i.CD, -1))
                        ));
                        break;
                    case "TSETV":
                        instrs.Add(new Assignment(
                            new IdentifierReference(_symbolTable.GetRegister(i.B), RegisterReference(i.CD)),
                            new IdentifierReference(_symbolTable.GetRegister(i.A))));
                        break;
                    case "TSETS":
                        instrs.Add(new Assignment(
                            new IdentifierReference(_symbolTable.GetRegister(i.B), ConvertLuaJitConstant(luaFunction.Constants[(int) i.CD], -1)),
                            new IdentifierReference(_symbolTable.GetRegister(i.A))));
                        break;
                    case "TSETB":
                        instrs.Add(new Assignment(
                            new IdentifierReference(_symbolTable.GetRegister(i.B), new Constant(i.CD, -1)),
                            new IdentifierReference(_symbolTable.GetRegister(i.A))));
                        break;
                    case "TSETM":
                        instrs.Add(new SetTableMultres(_symbolTable.GetRegister(i.A - 1), (int) i.CD + 1));
                        break;
                    
                    // Calls and vararg handling. T = tail call
                    case "CALLM":
                        args = new List<IExpression>();
                        var rets = new List<IdentifierReference>();
                        // Create references for a .. a + b - 2
                        if (i.B >= 2)
                        {
                            for (uint j = i.A; j <= i.A + i.B - 2; j++)
                                rets.Add(new IdentifierReference(_symbolTable.GetRegister(j)));
                        }
                        // Get references for a + 2 .. a + c
                        for (uint j = i.A + 2; j <= i.A + i.CD + 1; j ++)
                            args.Add(new IdentifierReference(_symbolTable.GetRegister(j)));
                        
                        var funcCall = new FunctionCall(new IdentifierReference(_symbolTable.GetRegister(i.A)), args);
                        funcCall.IsIndeterminateArgumentCount = (i.B == 0);
                        instrs.Add(new Assignment(rets, funcCall));
                        break;
                    case "CALL":
                        args = new List<IExpression>();
                        rets = new List<IdentifierReference>();
                        // Create references for a .. a + b - 2
                        if (i.B >= 2)
                        {
                            for (uint j = i.A; j <= i.A + i.B - 2; j++)
                                rets.Add(new IdentifierReference(_symbolTable.GetRegister(j)));
                        }
                        // Get references for a + 2 .. a + c
                        for (uint j = i.A + 2; j <= i.A + i.CD; j ++)
                            args.Add(new IdentifierReference(_symbolTable.GetRegister(j)));
                        
                        funcCall = new FunctionCall(new IdentifierReference(_symbolTable.GetRegister(i.A)), args);
                        funcCall.IsIndeterminateArgumentCount = (i.B == 0);
                        instrs.Add(new Assignment(rets, funcCall));
                        break;
                    case "CALLMT":
                        args = new List<IExpression>();
                        for (uint j = i.A + 2; j <= i.A + i.CD; j ++)
                            args.Add(new IdentifierReference(_symbolTable.GetRegister(j)));
                        
                        funcCall = new FunctionCall(new IdentifierReference(_symbolTable.GetRegister(i.A)), args);
                        funcCall.IsIndeterminateArgumentCount = (i.B == 0);
                        instrs.Add(new Return(funcCall));
                        break;
                    case "CALLT":
                        args = new List<IExpression>();
                        for (uint j = i.A + 2; j <= i.A + i.CD; j ++)
                            args.Add(new IdentifierReference(_symbolTable.GetRegister(j)));
                        
                        funcCall = new FunctionCall(new IdentifierReference(_symbolTable.GetRegister(i.A)), args);
                        instrs.Add(new Return(funcCall));
                        break;
                    case "ITERC":
                    case "ITERN":
                        args = new List<IExpression>();
                        rets = new List<IdentifierReference>();
                        // Create references for a .. a + b - 2
                        if (i.B >= 2)
                        {
                            for (uint j = i.A; j <= i.A + i.B - 2; j++)
                                rets.Add(new IdentifierReference(_symbolTable.GetRegister(j)));
                        }
                        // Get references for a + 2 .. a + c
                        for (uint j = i.A - 2; j <= i.A - 1; j ++)
                            args.Add(new IdentifierReference(_symbolTable.GetRegister(j)));
                        
                        funcCall = new FunctionCall(new IdentifierReference(_symbolTable.GetRegister(i.A - 3)), args);
                        funcCall.IsIndeterminateArgumentCount = (i.B == 0);
                        instrs.Add(new Assignment(rets, funcCall));
                        instrs.Add(new Jump(function.GetLabel((uint)(pos + 2)), new BinOp(RegisterReference(i.A), new Constant(ValueType.Nil, -1), BinOperationType.OpEqual)));
                        instrs.Add(new Assignment(_symbolTable.GetRegister(i.A - 1), new IdentifierReference(_symbolTable.GetRegister(i.A))));
                        break;
                    case "VARG":
                        rets = new List<IdentifierReference>();

                        for (uint j = i.A; j < i.A + i.B - 1; j++)
                            rets.Add(new IdentifierReference(_symbolTable.GetRegister(j)));
                        
                        instrs.Add(new Assignment(
                            rets,
                            new IdentifierReference(_symbolTable.GetVarargs())));
                        break;
                    case "ISNEXT":
                        instrs.Add(new Jump(function.GetLabel((uint) ((uint) pos + i.CD + 1))));
                        break;
                    
                    // Returns
                    case "RETM":
                        args = new List<IExpression>();
                        for (uint j = i.A; j < i.A + i.CD; j++)
                            args.Add(new IdentifierReference(_symbolTable.GetRegister(j)));
                        var rtrn = new Return(args) { IsIndeterminateReturnCount = i.B == 0 };
                        instrs.Add(rtrn);
                        break;
                    case "RET":
                        args = new List<IExpression>();
                        for (uint j = i.A; j < i.A + i.CD - 1; j++)
                            args.Add(new IdentifierReference(_symbolTable.GetRegister(j)));                            
                        instrs.Add(new Return(args));
                        break;
                    case "RET0":
                        instrs.Add(new Return());
                        break;
                    case "RET1":
                        instrs.Add(new Return(new IdentifierReference(_symbolTable.GetRegister(i.A))));
                        break;
                    
                    // Loops and branches. I/J = interp/JIT, I/C/L = init/call/loop
                    case "FORI":
                        instrs.Add(new Jump(function.GetLabel((uint) (pos + i.CD))));
                        break;
                    case "FORL":
                        instrs.Add(new Assignment(new IdentifierReference(_symbolTable.GetRegister(i.A)), new BinOp(new IdentifierReference(_symbolTable.GetRegister(i.A)),
                            new IdentifierReference(_symbolTable.GetRegister(i.A + 2)), BinOperationType.OpAdd)));
                        var jmp = new Jump(function.GetLabel((uint) (pos + 1 + i.CD)), new BinOp(new IdentifierReference(_symbolTable.GetRegister(i.A)),
                            new IdentifierReference(_symbolTable.GetRegister(i.A + 1)), BinOperationType.OpLoopCompare));
                        var pta = new Assignment(_symbolTable.GetRegister(i.A + 3), RegisterReference(i.A));
                        pta.PropagateAlways = true;
                        jmp.PostTakenAssignment = pta;
                        instrs.Add(jmp);
                        break;
                    case "LOOP":
                    case "ITERL":
                        instrs.Add(new Jump(function.GetLabel((uint) (pos + i.CD + 1))));
                        break;
                    case "JMP":
                        instrs.Add(new Jump(function.GetLabel((uint) ((uint) pos + i.CD + 1))));
                        break;
                    
                    default:
                        Console.WriteLine($@"Missing op: {i.OpCode.Name} {i.A} {i.B} {i.CD}");
                        instrs.Add(new PlaceholderInstruction($@"{i.OpCode.Name} {i.A} {i.B} {i.CD}"));
                        break;
                }
                instrs.ForEach(instr =>
                {
                    instr.OpLocation = pos;
                    function.Instructions.Add(instr);
                });
            }
        }
    }
}