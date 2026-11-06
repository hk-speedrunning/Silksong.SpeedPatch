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
        public bool CycleNormalizer = false;
    }

    private Settings _settings = new();
    private List<Patch> _patches;
    internal static string Version;

    public PatchesManager(ModuleDefinition _targetMainModule, ModuleDefinition _targetLocalizationModule, ModuleDefinition _sourceModule)
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
            new OnGUIPatch(_targetMainModule, _sourceModule, _settings),
            new UndeadnameJasminePatch(_targetLocalizationModule),
            new PatchNumberPatch(_targetModule),
        };

        if (_settings.DowndashTransitionFix)
        {
            _patches.Add(new DowndashPatch(_targetMainModule));
        }
        if (_settings.CourierRNGFix)
        {
            _patches.Add(new CourierFixPatch(_targetMainModule, _sourceModule));
        }
        if (_settings.CycleNormalizer)
        {
            _patches.Add(new CycleNormalizerPatch(_targetModule));
        }
    }

    public void ApplyPatches()
    {
        foreach (Patch patch in _patches)
        {
            Console.WriteLine($"Applying patch: {patch.GetType().Name}");
            patch.ApplyPatch();
        }
    }
}
