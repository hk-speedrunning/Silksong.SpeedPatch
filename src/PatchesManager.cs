using System;
using System.IO;
using System.Collections.Generic;
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
        [Serializable]
        internal struct SettingData
        {
            public bool Activated;
            [NonSerialized]
            public string Message;
        }

        public SettingData Downdash = new SettingData {
            Activated = false,
            Message = "Downdash Mod Activated",
        };

        public SettingData CourierFix = new SettingData {
            Activated = false,
            Message = "Courier Fix Mod Activated"
        };
    };

    private Settings _settings = new();
    private List<Patch> _patches;

    public PatchesManager(ModuleDefinition _targetModule, ModuleDefinition _sourceModule)
    {
        string modDir = Path.GetDirectoryName(_sourceModule.FileName);
        string configPath = Path.Combine(modDir, "DoostopPatchesConfig.json");
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
                _settings = serializer.Deserialize<Settings>(reader) ?? new();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"[ERROR]: {e}");
        }

        _patches = new List<Patch> {
            new OnGUIPatch(_targetModule, _sourceModule, _settings),
        };

        if (_settings.Downdash.Activated)
        {
            _patches.Add(new DowndashPatch(_targetModule));
        }
        if (_settings.CourierFix.Activated)
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
