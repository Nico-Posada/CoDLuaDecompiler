using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.CommandLine;
using System.CommandLine.Invocation;
using CoDLuaDecompiler.AssetExporter;
using CoDLuaDecompiler.Decompiler;
using CoDLuaDecompiler.Decompiler.LuaFile;
using CoDLuaDecompiler.Common;
using System.Reflection;
using System.CommandLine.Parsing;

namespace CoDLuaDecompiler.CLI;

class Program
{
    private readonly IAssetExport _assetExport;
    private readonly IDecompiler _decompiler;
    public static bool UsesDebugInfo = false;

    public Program(IDecompiler decompiler, IAssetExport assetExport)
    {
        _decompiler = decompiler;
        _assetExport = assetExport;
    }

    private void HandleFile(string filePath)
    {
        try
        {
            // parse lua file
            var file = LuaFileFactory.Create(filePath, UsesDebugInfo);

#if DEBUG
            Console.WriteLine($"Decompiling file: {filePath}");
#endif

            // decompile file
            var output = _decompiler.Decompile(file);

            string outFileName;
            if (AppInfo.OutputDirectory is null)
            {
                // replace extension
                outFileName = Path.ChangeExtension(filePath, ".dec.lua");
            }
            else
            {
                // get basename and join with specified output directory
                string basename = Path.GetFileName(filePath);
                outFileName = Path.Join(AppInfo.OutputDirectory, basename);
            }

            // save output
            File.WriteAllText(outFileName, output);

            Console.WriteLine($"Decompiled file: {filePath}");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error while decompiling file: {filePath}");
            Console.WriteLine(e);
        }
    }
    private static List<string> ParseFilesFromArgs(IEnumerable<string> args)
    {
        var files = new List<string>();

        string luaExtension = !UsesDebugInfo ? "*.lua*" : "*.luac";

        foreach (var arg in args)
        {
            Console.WriteLine($"Checking if {arg} exists!");
            if (!File.Exists(arg) && !Directory.Exists(arg))
                continue;

            Console.WriteLine($"{arg} exists!");
            var attr = File.GetAttributes(arg);
            // determine if we're a directory first
            // if so only includes file that are of ".lua" or ".luac" extension
            if (attr.HasFlag(FileAttributes.Directory))
            {
                files.AddRange(Directory.GetFiles(arg, luaExtension, SearchOption.AllDirectories).ToList());
            }
            else if (Path.GetExtension(arg).Contains(".lua"))
            {
                files.Add(arg);
            }
            else
            {
                Console.WriteLine($"Invalid argument passed {arg} | {File.GetAttributes(arg)}!");
            }
        }

        // make sure to remove duplicates
        files = files.Distinct().ToList();

        // also remove any already dumped files
        files.RemoveAll(elem => elem.EndsWith(".dec.lua"));
        files.RemoveAll(elem => elem.EndsWith(".luadec"));

        return files;
    }

    public void Run(
        string? outputDir,
        bool doExport, bool doRawDump,
        bool debug,
        bool funcStats,
        IEnumerable<string> args)
    {
        if (doExport)
        {
            Console.WriteLine("Starting asset export from memory.");
            _assetExport.ExportAssets(doRawDump);
        }

        AppInfo.ShowFunctionData = funcStats;
        UsesDebugInfo = debug;

        if (outputDir is not null)
        {
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            AppInfo.OutputDirectory = outputDir;
        }

        Console.WriteLine($"Files: {String.Join(", ", args)}");

        // parse files from arguments
        var files = ParseFilesFromArgs(args);

        Console.WriteLine($"Total of {files.Count} to process.");

#if DEBUG
            files.ForEach(HandleFile);
#else
        Parallel.ForEach(files, HandleFile);
#endif
    }

    public async Task<int> Main(string[] args)
    {
        var outDirOption = new Option<string?>(
            aliases: new string[] { "--output-dir", "-o" },
            description: "Specify the directory to output decompiled lua files. If none is provided, " +
                         "it will write to the same location as the input lua file.");

        var doExport = new Option<bool>(
            name: "--export",
            description: "Export luas from a running game's memory (only for older titles).",
            getDefaultValue: () => false);

        var doRawDump = new Option<bool>(
            name: "--dump",
            description: "If --export is enabled, then enabling this will export the raw lua files along with the decompiled lua files.",
            getDefaultValue: () => false);

        var debug = new Option<bool>(
            name: "--debug",
            description: "Will extract debug information from T7 luas. Will change the extension to look for from *.lua to *.luac.",
            getDefaultValue: () => false);

        var funcStats = new Option<bool>(
            aliases: new string[] { "--functionstats", "-fs" },
            description: "Prepends function information to decompiled functions.",
            getDefaultValue: () => false);

        var luasPath = new Argument<IEnumerable<string>>(
            name: "paths",
            description: "A list of 1 or more paths to a directory of luas or a single lua file.");


        var rootCommand = new RootCommand("Lua decompiler for Call of Duty games.");
        rootCommand.AddOption(outDirOption);
        rootCommand.AddOption(funcStats);
        rootCommand.AddOption(doExport);
        rootCommand.AddOption(doRawDump);
        rootCommand.AddOption(debug);
        rootCommand.AddArgument(luasPath);

        rootCommand.SetHandler(Run,
            outDirOption, funcStats, doExport, doRawDump, debug, luasPath);

        return await rootCommand.InvokeAsync(args);
    }
}
