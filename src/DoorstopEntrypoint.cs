using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SilksongDoorstop;

// ReSharper disable once CheckNamespace
namespace Doorstop;

public static class Entrypoint
{
    public static void Start()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string dataDir = Directory.GetDirectories(baseDir, "*_Data").FirstOrDefault();
        string managedDir = Path.Combine(baseDir, dataDir, "Managed");
        string asmPath = Path.Combine(managedDir, "Assembly-CSharp.dll");

        string modPath = typeof(Entrypoint).Assembly.Location;
        string modDir = Path.GetDirectoryName(modPath);

        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(managedDir);
        resolver.AddSearchDirectory(modPath);
        resolver.AddSearchDirectory(typeof(object).Assembly.Location);

        ReaderParameters readerParams = new ReaderParameters
        {
            AssemblyResolver = resolver,
            ReadWrite = false
        };

        AssemblyDefinition silksongAsm = AssemblyDefinition.ReadAssembly(asmPath, readerParams);
        AssemblyDefinition modAsm = AssemblyDefinition.ReadAssembly(modPath, readerParams);

        ModuleDefinition silksongModule = silksongAsm.MainModule;
        ModuleDefinition modModule = modAsm.MainModule;

        PatchesManager manager = new(silksongModule, modModule);
        manager.ApplyPatches();

        silksongAsm.Write(Path.Combine(modDir, "Assembly-CSharp.dll"));

        // Cleanup resources
        silksongAsm.Dispose();
        modAsm.Dispose();
    }
}
