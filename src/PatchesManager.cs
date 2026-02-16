using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SilksongDoorstop.Patches;

namespace SilksongDoorstop;

internal class PatchesManager
{
    internal class Settings
    {
        internal struct SettingData
        {
            public bool activated;
            public string message;
        }

        internal Dictionary<string, SettingData> data = new();

        internal SettingData Downdash
        {
            get
            {
                return data.GetValueOrDefault("downdash", new SettingData
                {
                    activated = false,
                    message = "Downdash Mod Activated"
                });
            }
            set
            {
                if (data.TryGetValue("downdash", out SettingData downdash))
                {
                    downdash.activated = value.activated;
                    downdash.message = value.message;
                }
                else
                {
                    data.Add("downdash", value);
                }
            }
        }

        internal SettingData CourierFix
        {
            get
            {
                return data.GetValueOrDefault("courierfix", new SettingData
                {
                    activated = false,
                    message = "Courier Fix Mod Activated"
                });
            }
            set
            {
                if (data.TryGetValue("courierfix", out SettingData courierfix))
                {
                    courierfix.activated = value.activated;
                    courierfix.message = value.message;
                }
                else
                {
                    data.Add("courierfix", value);
                }
            }
        }
    };

    private Settings _settings;
    private List<Patch> _patches;

    public PatchesManager(ModuleDefinition _targetModule, ModuleDefinition _sourceModule)
    {
        _settings = new();

        _patches = new List<Patch> {
            new OnGUIPatch(_targetModule, _sourceModule, _settings),
        };

        if (_settings.Downdash.activated)
        {
            _patches.Add(new DowndashPatch(_targetModule));
        }
        if (_settings.CourierFix.activated)
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
