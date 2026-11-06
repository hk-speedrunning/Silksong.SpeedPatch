using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SilksongDoorstop;

namespace SilksongDoorstop.Patches;

internal class PatchNumberPatch: Patch
{
    private ModuleDefinition _targetModule;

    protected MethodDefinition _targetMethod;

    public PatchNumberPatch(ModuleDefinition targetModule)
    {
        _targetModule = targetModule;
        TypeDefinition targetType = _targetModule.GetType("SetVersionNumber");

        _targetMethod = targetType.Methods.First(method => method.Name == "Start");
    }

    public void ApplyPatch()
    {
        ILProcessor il = _targetMethod.Body.GetILProcessor();

        Instruction toReplace = il.Body.Instructions.First(inst =>
            inst.OpCode == OpCodes.Ldstr &&
            ((string)inst.Operand).StartsWith("1.")
        );
        string oldValue = (string)toReplace.Operand;
        il.Replace(toReplace, il.Create(OpCodes.Ldstr, oldValue + "-SP"));
    }
}
