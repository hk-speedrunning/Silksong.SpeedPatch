using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SilksongDoorstop;

namespace SilksongDoorstop.Patches;

internal class IntroSkipPatch : Patch
{
    private ModuleDefinition _targetModule;
    private TypeDefinition _targetType;
    private MethodDefinition _targetMethod;

    public IntroSkipPatch(ModuleDefinition targetModule)
    {
        _targetModule = targetModule;
        _targetType = _targetModule.GetType("OpeningSequence");

        _targetMethod = _targetType.Methods.First(method => method.Name == "Update");
    }

    public void ApplyPatch()
    {
        ILProcessor il = _targetMethod.Body.GetILProcessor();

        FieldDefinition chainSequenceField = _targetType.Fields.First(field => field.Name == "chainSequence");

        TypeDefinition chainSequenceType = _targetModule.GetType("ChainSequence");
        MethodDefinition isCurrentSkipped = chainSequenceType.Methods.First(method => method.Name == "get_IsCurrentSkipped");
        MethodDefinition skipSingle = chainSequenceType.Methods.First(method => method.Name == "SkipSingle");

        Instruction insertBefore = il.Body.Instructions[0];
        il.InsertBefore(insertBefore, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(insertBefore, il.Create(OpCodes.Ldfld, chainSequenceField));
        il.InsertBefore(insertBefore, il.Create(OpCodes.Callvirt, isCurrentSkipped));
        il.InsertBefore(insertBefore, il.Create(OpCodes.Brtrue_S, insertBefore));
        il.InsertBefore(insertBefore, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(insertBefore, il.Create(OpCodes.Ldfld, chainSequenceField));
        il.InsertBefore(insertBefore, il.Create(OpCodes.Callvirt, skipSingle));
    }
}
