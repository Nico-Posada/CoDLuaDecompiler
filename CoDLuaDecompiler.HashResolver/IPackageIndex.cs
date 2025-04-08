using System.Collections.Generic;

namespace CoDLuaDecompiler.HashResolver;

public interface IPackageIndex
{
    void Load(ulong hashMask);
    Dictionary<ulong, string> GetEntries(ulong hashMask);
}
