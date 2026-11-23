using System;
using System.IO;
using System.Linq;
using Il2CppInterop.Generator;
using Il2CppInterop.Generator.Runners;
using Il2CppInterop.Common;
using Microsoft.Extensions.Logging;

// Uso: InteropGen <pasta com DLLs dummy do AmongUs.GameLibs.Android> <pasta unity-libs> <saída>
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 3) { Console.Error.WriteLine("uso: InteropGen <dummy> <unity-libs> <out>"); return 2; }
        var source = Directory.EnumerateFiles(args[0], "*.dll")
            .Select(f => AsmResolver.DotNet.AssemblyDefinition.FromFile(f))
            .ToList();
        Directory.CreateDirectory(args[2]);
        var options = new GeneratorOptions
        {
            Source = source,
            OutputDir = args[2],
            UnityBaseLibsDir = args[1],
            NoXrefCache = true,
            Parallel = true,
        };
        using var factory = LoggerFactory.Create(_ => { });
        Il2CppInteropGenerator.Create(options).AddLogger(factory.CreateLogger("Il2CppInteropGen"))
            .AddInteropAssemblyGenerator().Run();
        Console.WriteLine($"OK: {Directory.GetFiles(args[2], "*.dll").Length} assemblies em {args[2]}");
        return 0;
    }
}
