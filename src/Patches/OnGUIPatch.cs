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
        foreach (FieldInfo field in settings.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            PatchesManager.Settings.SettingData data = (PatchesManager.Settings.SettingData)
                field.GetValue(settings);
            if (data.Activated)
            {
                _warningText += data.Message + '\n';
            }
        }

        _warningText += "Doorstop Patches";
    }

    override public void ApplyPatch()
    {
        ILProcessor il = _targetMethod.Body.GetILProcessor();
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
                    fontSize = 30,
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
