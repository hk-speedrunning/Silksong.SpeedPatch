using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using SilksongDoorstop;

// ReSharper disable once CheckNamespace
namespace Doorstop;

public static class Entrypoint
{
    public static void Start()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dataDir = Directory.GetDirectories(baseDir, "*_Data").FirstOrDefault();

            // Check for MacOS directory structure
            // that is different from Windows and Linux
            if (String.IsNullOrEmpty(dataDir))
            {
                string contentsDir = Path.GetDirectoryName(baseDir);
                dataDir = Path.Combine(contentsDir, "Resources", "Data");
            }

            string managedDir = Path.Combine(baseDir, dataDir, "Managed");
            string asmMainPath = Path.Combine(managedDir, "Assembly-CSharp.dll");
            string asmLocalizationPath = Path.Combine(managedDir, "TeamCherry.Localization.dll");

            string modPath = typeof(Entrypoint).Assembly.Location;
            string modDir = Path.GetDirectoryName(modPath)!;
            string outputPath = Path.Combine(modDir, "Assembly-CSharp.dll");

            File.Delete(outputPath);

            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(managedDir);
            resolver.AddSearchDirectory(modPath);
            resolver.AddSearchDirectory(typeof(object).Assembly.Location);

            ReaderParameters readerParams = new ReaderParameters
            {
                AssemblyResolver = resolver,
                ReadWrite = false
            };

            using AssemblyDefinition silksongAsm = AssemblyDefinition.ReadAssembly(asmMainPath, readerParams);
            using AssemblyDefinition localizationAsm = AssemblyDefinition.ReadAssembly(asmLocalizationPath, readerParams);
            using AssemblyDefinition modAsm = AssemblyDefinition.ReadAssembly(modPath, readerParams);

            ModuleDefinition silksongModule = silksongAsm.MainModule;
            ModuleDefinition localizationModule = localizationAsm.MainModule;
            ModuleDefinition modModule = modAsm.MainModule;

            PatchesManager manager = new(silksongModule, localizationModule, modModule);
            manager.ApplyPatches();

            silksongAsm.Write(Path.Combine(modDir, "Assembly-CSharp.dll"));
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}
