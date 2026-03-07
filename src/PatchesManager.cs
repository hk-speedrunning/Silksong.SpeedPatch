using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Newtonsoft.Json;
using SilksongDoorstop.Patches;

namespace SilksongDoorstop;

internal class PatchesManager
{
    [Serializable]
    internal class Settings
    {
        public bool DowndashTransitionFix = false;
        public bool CourierRNGFix = false;
    }

    private Settings _settings = new();
    private List<Patch> _patches;
    internal static string Version;

    public PatchesManager(ModuleDefinition _targetModule, ModuleDefinition _sourceModule)
    {
        Version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            .InformationalVersion.Split('+')[0];
        
        string modDir = Path.GetDirectoryName(_sourceModule.FileName);
        string configPath = Path.Combine(modDir, "hksr_patches.json");
        try 
        {
            if (!File.Exists(configPath))
            {
                using (StreamWriter file = File.CreateText(configPath))
                using (JsonWriter writer = new JsonTextWriter(file))
                {
                    JsonSerializer serializer = new JsonSerializer
                    {
                        Formatting = Formatting.Indented,
                    };
                    serializer.Serialize(writer, _settings);
                }
            }

            using (StreamReader file = File.OpenText(configPath))
            using (JsonReader reader = new JsonTextReader(file))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Populate(reader, _settings);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ERROR]: {e}");
        }

        _patches = new List<Patch> {
            new OnGUIPatch(_targetModule, _sourceModule, _settings),
        };

        if (_settings.DowndashTransitionFix)
        {
            _patches.Add(new DowndashPatch(_targetModule));
        }
        if (_settings.CourierRNGFix)
        {
            _patches.Add(new CourierFixPatch(_targetModule, _sourceModule));
        }
    }

    public void ApplyPatches()
    {
        foreach (Patch patch in _patches)
        {
            patch.ApplyPatch();
        }
    }
}
