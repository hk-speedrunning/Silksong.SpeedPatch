using System;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEngine;
using SilksongDoorstop;

namespace SilksongDoorstop.Patches;

internal class OnGUIPatch : CopyPatch
{
    private readonly string _warningText;

    public OnGUIPatch(ModuleDefinition targetModule, ModuleDefinition sourceModule, PatchesManager.Settings settings)
        : base(targetModule, sourceModule, "GameManager", "OnGUI")
    {
        _warningText = $"SpeedPatch v{PatchesManager.Version!}\n";

        _warningText += string.Join(", ", settings.GetShortcodes());
        
        // if (settings.DowndashTransitionFix) _warningText += "DowndashTransitionFix\n";
        // if (settings.CourierRNGFix) _warningText += "CourierRNGFix\n";
        // if (settings.CycleNormalizer) _warningText += "CycleNormalizer\n";
        
    }

    public override void ApplyPatch()
    {
        ILProcessor il = TargetMethod.Body.GetILProcessor();
        il.Clear();

        base.ApplyPatch();

        Instruction toReplace = il.Body.Instructions.First(inst =>
            inst.OpCode == OpCodes.Ldstr &&
            ((string)inst.Operand) == "<PlaceHolder>"
        );

        il.Replace(toReplace, il.Create(OpCodes.Ldstr, _warningText));
    }
}

internal class GameManager : global::GameManager
{
    private void OnGUI()
    {
        string WarningText = "<PlaceHolder>";

        if (GetSceneNameString() == Constants.MENU_SCENE)
        {
            var oldBackgroundColor = GUI.backgroundColor;
            var oldContentColor = GUI.contentColor;
            var oldColor = GUI.color;
            var oldMatrix = GUI.matrix;

            GUI.backgroundColor = Color.white;
            GUI.contentColor = Color.white;
            GUI.color = Color.white;
            GUI.matrix = Matrix4x4.TRS(
                Vector3.zero,
                Quaternion.identity,
                new Vector3(Screen.width / 1920f, Screen.height / 1080f, 1f)
            );

            GUI.Label(
                new Rect(20f, 20f, 200f, 200f),
                WarningText,
                new GUIStyle
                {
                    fontSize = 24,
                    normal = new GUIStyleState
                    {
                        textColor = Color.white,
                    }
                }
            );

            GUI.backgroundColor = oldBackgroundColor;
            GUI.contentColor = oldContentColor;
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }
    }
}
