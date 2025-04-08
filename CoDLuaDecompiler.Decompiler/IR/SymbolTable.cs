using System;
using System.Collections.Generic;
using System.Linq;
using CoDLuaDecompiler.Decompiler.IR.Identifiers;
using ValueType = CoDLuaDecompiler.Decompiler.IR.Identifiers.ValueType;

namespace CoDLuaDecompiler.Decompiler.IR
{
    public class SymbolTable
    {
        public readonly Stack<Dictionary<String, Identifier>> ScopeStack;
        // Use this stack for debugging
        private readonly List<Dictionary<String, Identifier>> _finalizedScopes;

        public SymbolTable()
        {
            ScopeStack = new Stack<Dictionary<string, Identifier>>();
            _finalizedScopes = new List<Dictionary<string, Identifier>>();
            // Add a stack for the globals
            ScopeStack.Push(new Dictionary<string, Identifier>());
        }
        
        public void BeginScope()
        {
            ScopeStack.Push(new Dictionary<string, Identifier>());
        }
        
        public void EndScope()
        {
            _finalizedScopes.Add(ScopeStack.Pop());
        }
        
        public Identifier GetRegister(uint reg)
        {
            if (!ScopeStack.Peek().ContainsKey($@"REG{reg}"))
            {
                Identifier id = new Identifier
                {
                    IdentifierType = IdentifierType.Register,
                    ValueType = ValueType.Unknown,
                    Name = $@"REG{reg}",
                    RegNum = reg
                };
                ScopeStack.Peek().Add(id.Name, id);
            }
            return ScopeStack.Peek()[$@"REG{reg}"];
        }

        public Identifier GetNewRegister()
        {
            // Get the highest regs
            var newRegNum = ScopeStack.Peek()
                    .Select(st => st.Key.StartsWith("REG") && int.TryParse(st.Key.AsSpan(3), out int r) ? r : -1)
                    .Where(reg => reg != -1)
                    .Max() + 1;

            Identifier id = new Identifier
            {
                IdentifierType = IdentifierType.Register,
                ValueType = ValueType.Unknown,
                Name = $@"REG{newRegNum}",
                RegNum = (uint) newRegNum
            };
            ScopeStack.Peek().Add(id.Name, id);

            return id;
        }
        
        public Identifier GetUpValue(uint upValue, string varName = "")
        {
            if (!ScopeStack.Peek().ContainsKey($@"UPVAL{upValue}"))
            {
                Identifier id = new Identifier
                {
                    IdentifierType = IdentifierType.Upvalue,
                    ValueType = ValueType.Unknown,
                    Name = $@"UPVAL{upValue}"
                };
                if (!String.IsNullOrEmpty(varName))
                    id.UpvalueVarName = varName;
                ScopeStack.Peek().Add(id.Name, id);
            }
            return ScopeStack.Peek()[$@"UPVAL{upValue}"];
        }

        public Identifier GetGlobal(string global, int constantId)
        {
            if (!ScopeStack.First().ContainsKey(global))
            {
                Identifier id = new Identifier
                {
                    IdentifierType = IdentifierType.Global,
                    ValueType = ValueType.Unknown,
                    Name = global,
                    ConstantId = constantId
                };
                ScopeStack.First().Add(id.Name, id);
            }
            return ScopeStack.First()[global];
        }

        public Identifier GetVarargs()
        {
            if (!ScopeStack.First().ContainsKey("$VARARGS"))
            {
                Identifier id = new Identifier
                {
                    IdentifierType = IdentifierType.Varargs, ValueType = ValueType.Unknown, Name = "$VARARGS"
                };
                ScopeStack.First().Add(id.Name, id);
            }
            return ScopeStack.First()["$VARARGS"];
        }
        
        public HashSet<Identifier> GetAllRegistersInScope()
        {
            var ret = new HashSet<Identifier>();
            foreach (var reg in ScopeStack.Peek())
            {
                if (reg.Value.IdentifierType == IdentifierType.Register)
                {
                    ret.Add(reg.Value);
                }
            }
            return ret;
        }
    }
}