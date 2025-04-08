using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using CoDLuaDecompiler.AssetExporter.Games;
using CoDLuaDecompiler.AssetExporter.Util;
using CoDLuaDecompiler.Common;
using CoDLuaDecompiler.Decompiler;
using CoDLuaDecompiler.Decompiler.LuaFile;
using CoDLuaDecompiler.HashResolver;

namespace CoDLuaDecompiler.AssetExporter;

public class AssetExport : IAssetExport
{
    private readonly IDecompiler _decompiler;
    private readonly Dictionary<ulong, string> _hashEntries;
    public static ProcessReader Reader { get; set; }

    public AssetExport(IDecompiler decompiler, IPackageIndex packageIndex)
    {
        _decompiler = decompiler;
        _hashEntries = packageIndex.GetEntries(AppInfo.HashMask);
    }
    
    public static Dictionary<string, Tuple<IGame, bool>> Games = new Dictionary<string, Tuple<IGame, bool>>()
    {
        { "BlackOps3",      new Tuple<IGame, bool>(new BlackOps3(),    true) },
        { "BlackOps4",      new Tuple<IGame, bool>(new BlackOps4(),    true) },
        { "BlackOpsColdWar",      new Tuple<IGame, bool>(new BlackOpsColdWar(),    true) },
        //{ "ModernWarfare",      new Tuple<IGame, bool>(new ModernWarfare(),    true) },
        //{ "Vanguard",      new Tuple<IGame, bool>(new Vanguard(),    true) },
        { "s2_mp64_ship",      new Tuple<IGame, bool>(new WW2(),    true) },
        { "s2_sp64_ship",      new Tuple<IGame, bool>(new WW2(),    true) },
    };
    
    public void ExportAssets(bool dumpRaw = false)
    {
        Process[] processes = Process.GetProcesses();
        
        foreach(var process in processes)
        {
            // Check process name against game list
            if(Games.ContainsKey(process.ProcessName))
            {
                // Result
                var game = Games[process.ProcessName];
                // Info
                Console.WriteLine("Found matching game {0}", process.ProcessName);
                // Set Reader
                Reader = new ProcessReader(process);
                // Load Game
                var files = game.Item1.LoadLuaFiles(game.Item2);
                if (files == null)
                    Console.WriteLine("This game is supported, but this update is not.");
                else
                    HandleLuaFiles(files, game.Item1, dumpRaw);
                // Done
                return;
            }
        }

        // Failed
        Console.WriteLine("Failed to find a supported game");
    }

    private static MD5 md5 = MD5.Create();
    private string GetHash(LuaFileData luaFile)
    {
        var hash = md5.ComputeHash(luaFile.Reader.BaseStream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    private void HandleLuaFiles(List<LuaFileData> luaFiles, IGame game, bool dumpRaw = false)
    {
        Parallel.ForEach(luaFiles, file =>
        {
            string filePath = file.Name;
            if (String.IsNullOrEmpty(filePath))
            {
                ulong hashNumber = (ulong) (file.Hash & 0xFFFFFFFFFFFFFFF);

                if (_hashEntries.ContainsKey(hashNumber))
                    filePath = _hashEntries[hashNumber];
                else
                    filePath = String.Format("Luafile_{0:x}", hashNumber);
            }
                

            var directory = Path.GetDirectoryName(game.ExportFolder + filePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory!);
                
            var outFileName = Path.ChangeExtension(game.ExportFolder + filePath, ".dec.lua");
            
            // Check if we already decompiled the file
            string hash = "";
            if (File.Exists(outFileName))
            {
                string line = File.ReadLines(outFileName).FirstOrDefault();
                if (!String.IsNullOrEmpty(line) && line.StartsWith("-- "))
                {
                    var checksum = line.Remove(0, 3);
                    hash = GetHash(file);
                    if (checksum == hash)
                        return;
                }
            }

            if (dumpRaw)
            {
                using (var fileStream = File.Create(Path.ChangeExtension(game.ExportFolder + filePath, ".luac")))
                {
                    file.Reader.BaseStream.Seek(0, SeekOrigin.Begin);
                    file.Reader.BaseStream.CopyTo(fileStream);
                }
            }
            
            var luaFile = LuaFileFactory.Create(file.Reader, null);
            try
            {
                if (String.IsNullOrEmpty(hash))
                    hash = GetHash(file);
                var prefix = $"-- {hash}\n-- This hash is used for caching, delete to decompile the file again\n\n";
                var decompiledFile = _decompiler.Decompile(luaFile);
                
                // save output
                File.WriteAllText(outFileName, prefix + decompiledFile);
                Console.WriteLine($"Decompiled file: {Path.GetFileName(filePath)}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error while trying to decompile file {Path.GetFileName(filePath)}: {e.Message}");
            }
        });
    }
}
