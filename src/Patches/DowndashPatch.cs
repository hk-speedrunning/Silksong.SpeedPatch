using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SilksongDoorstop;
using UnityEngine;

namespace SilksongDoorstop.Patches;

internal class DowndashPatch : Patch
{
    private readonly MethodDefinition _targetMethod;

    public DowndashPatch(ModuleDefinition targetModule)
    {
        TypeDefinition targetType = targetModule.GetType("HeroController");

        TypeDefinition statemachineType = targetType.NestedTypes.First(type => type.Name.StartsWith("<EnterScene>d__"));
        _targetMethod = statemachineType.Methods.First(method => method.Name == "MoveNext");
    }

    public void ApplyPatch()
    {
        ILProcessor il = _targetMethod.Body.GetILProcessor();

        Instruction toReplace = il.Body.Instructions.First(inst =>
            inst.OpCode == OpCodes.Ldc_R4 &&
            Mathf.Approximately((float)inst.Operand, 0.33f)
        );
        il.Replace(toReplace, il.Create(OpCodes.Ldc_R4, 0.34f));
    }
}
