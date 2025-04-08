using System;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using CoDLuaDecompiler.AssetExporter;
using CoDLuaDecompiler.Decompiler;
using CoDLuaDecompiler.Decompiler.LuaFile;
using CoDLuaDecompiler.HashResolver;

namespace CoDLuaDecompiler.CLI;

public class Startup
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("CoD Lua Decompiler");

        // setup dependency injection
        var builder = new ContainerBuilder();

        // CoDHavokTool.Common
        builder.Register((context, parameters) =>
        {
            if (parameters.Count() != 1) throw new ArgumentOutOfRangeException();

            return LuaFileFactory.Create(parameters.Positional<string>(0));
        }).As<ILuaFile>().InstancePerLifetimeScope();

        // CoDHavokTool.LuaDecompiler
        builder.RegisterType<Decompiler.Decompiler>().As<IDecompiler>().SingleInstance();

        // CodHavokTool
        builder.RegisterType<GithubUpdateChecker>().SingleInstance();
        builder.RegisterType<AssetExport>().As<IAssetExport>().SingleInstance();
        builder.RegisterType<PackageIndex>().As<IPackageIndex>().SingleInstance();
        builder.RegisterType<Program>().SingleInstance();

        var container = builder.Build();
        var updateTask = container.Resolve<GithubUpdateChecker>().CheckForUpdate();
        await container.Resolve<Program>().Main(args);
        await updateTask;

        Console.WriteLine("Press enter to exit");
        Console.ReadLine();
    }
}
