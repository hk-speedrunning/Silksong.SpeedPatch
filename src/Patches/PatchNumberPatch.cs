using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SilksongDoorstop;

namespace SilksongDoorstop.Patches;

internal class PatchNumberPatch: Patch
{
    private MethodDefinition _targetMethod;

    private readonly string _shortcodeString;

    public PatchNumberPatch(ModuleDefinition targetModule, PatchesManager.Settings settings)
    {
        var targetType = targetModule.GetType("SetVersionNumber");

        _targetMethod = targetType.Methods.First(method => method.Name == "Start");

        _shortcodeString = string.Join(", ", settings.GetShortcodes());
    }

    public void ApplyPatch()
    {
        ILProcessor il = _targetMethod.Body.GetILProcessor();

        Instruction toReplace = il.Body.Instructions.First(inst =>
            inst.OpCode == OpCodes.Ldstr &&
            ((string)inst.Operand).StartsWith("1.")
        );
        string oldValue = (string)toReplace.Operand;
        il.Replace(toReplace, il.Create(OpCodes.Ldstr, oldValue + $"-S{PatchesManager.Version}\n{_shortcodeString}"));
    }
}
