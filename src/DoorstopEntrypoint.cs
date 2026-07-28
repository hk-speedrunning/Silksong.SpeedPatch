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

        // Check for MacOS directory structure
        // that is different from Windows and Linux
        if (String.IsNullOrEmpty(dataDir)) {
            string contentsDir = Path.GetDirectoryName(baseDir);
            dataDir = Path.Combine(contentsDir, "Resources", "Data");
        }
        string managedDir = Path.Combine(baseDir, dataDir, "Managed");
        string asmMainPath = Path.Combine(managedDir, "Assembly-CSharp.dll");
        string asmLocalizationPath = Path.Combine(managedDir, "TeamCherry.Localization.dll");

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

        AssemblyDefinition silksongAsm = AssemblyDefinition.ReadAssembly(asmMainPath, readerParams);
        AssemblyDefinition localizationAsm = AssemblyDefinition.ReadAssembly(asmLocalizationPath, readerParams);
        AssemblyDefinition modAsm = AssemblyDefinition.ReadAssembly(modPath, readerParams);

        ModuleDefinition silksongModule = silksongAsm.MainModule;
        ModuleDefinition localizationModule = localizationAsm.MainModule;
        ModuleDefinition modModule = modAsm.MainModule;

        PatchesManager manager = new(silksongModule, localizationModule, modModule);
        manager.ApplyPatches();

        silksongAsm.Write(Path.Combine(modDir, "Assembly-CSharp.dll"));
        localizationAsm.Write(Path.Combine(modDir, "TeamCherry.Localization.dll"));

        // Cleanup resources
        silksongAsm.Dispose();
        localizationAsm.Dispose();
        modAsm.Dispose();
    }
}
