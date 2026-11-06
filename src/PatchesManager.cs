using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Mono.Cecil;
using Newtonsoft.Json;
using SilksongDoorstop.Patches;

namespace SilksongDoorstop;

internal class PatchesManager
{
    [Serializable]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal class Settings
    {
        [Serializable]
        internal class NmgPatches
        {
            public bool CourierRNGFix;
            public bool CycleNormalizer;
        }
        public NmgPatches NMGPatches = new();
        
        [Serializable]
        internal class AgPatches
        {
            public bool DowndashTransitionFix;
        }
        public AgPatches AGPatches = new();
        
        public bool AltDisplay;
        
        internal const float CycleDelay = 0.06f; // May later become configurable.

        public IEnumerable<string> GetShortcodes()
        {
            List<string> shortcodes = [];
            if (AGPatches.DowndashTransitionFix) shortcodes.Add("DDTF");
            if (NMGPatches.CourierRNGFix) shortcodes.Add("CRNG");
            if (NMGPatches.CycleNormalizer) shortcodes.Add($"CYC-{CycleDelay*100}");
            return shortcodes;
        }

        internal bool IsNmg => AGPatches.DowndashTransitionFix;
    }

    private Settings _settings = new();
    private List<Patch> _patches;
    internal static string Version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        .InformationalVersion.Split('+')[0].Replace("-", "");

    public PatchesManager(ModuleDefinition _targetMainModule, ModuleDefinition _targetLocalizationModule, ModuleDefinition _sourceModule)
    {
        string modDir = Path.GetDirectoryName(_sourceModule.FileName)!;
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

        _patches =
        [
            new UndeadnameJasminePatch(_targetLocalizationModule),
            new PatchNumberPatch(_targetMainModule, _settings)
        ];

        if (_settings.AltDisplay) _patches.Add(new OnGUIPatch(_targetMainModule, _sourceModule, _settings));

        if (_settings.AGPatches.DowndashTransitionFix) _patches.Add(new DowndashPatch(_targetMainModule));
        if (_settings.NMGPatches.CourierRNGFix) _patches.Add(new CourierFixPatch(_targetMainModule, _sourceModule));
        if (_settings.NMGPatches.CycleNormalizer) _patches.Add(new CycleNormalizerPatch(_targetMainModule, Settings.CycleDelay));
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
