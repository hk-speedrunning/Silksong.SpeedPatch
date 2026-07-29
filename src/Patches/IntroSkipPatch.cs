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
    private TypeDefinition _syncType;
    private TypeDefinition _asyncType;

    public IntroSkipPatch(ModuleDefinition targetModule)
    {
        _targetModule = targetModule;
        _targetType = _targetModule.GetType("OpeningSequence");

        _syncType = _targetType.NestedTypes.First(type => type.Name.StartsWith("<StartSync>d__"));
        _asyncType = _targetType.NestedTypes.First(type => type.Name.StartsWith("<StartAsync>d__"));
    }

    public void ApplyPatch()
    {
        PatchCoroutine(_syncType);
        PatchCoroutine(_asyncType);
    }

    private void PatchCoroutine(TypeDefinition type) {
        MethodDefinition targetMethod = type.Methods.First(method => method.Name == "MoveNext");

        ILProcessor il = targetMethod.Body.GetILProcessor();

        int startIdx = 0;
        int endIdx = 0;
        for (int idx = 1; idx < il.Body.Instructions.Count - 1; idx++)
        {
            Instruction prev = il.Body.Instructions[idx - 1];
            Instruction curr = il.Body.Instructions[idx];
            Instruction next = il.Body.Instructions[idx + 1];

            bool prevCorrectStart = prev.OpCode == OpCodes.Ldloc_1;
            bool currCorrectStart = curr.OpCode == OpCodes.Ldfld 
                && ((MemberReference)curr.Operand).FullName == "ChainSequence OpeningSequence::chainSequence";
            bool nextCorrectStart = next.OpCode == OpCodes.Callvirt 
                && ((MemberReference)next.Operand).FullName == "System.Boolean ChainSequence::get_IsCurrentSkipped()";

            bool prevCorrectEnd = prev.OpCode == OpCodes.Ldloc_3;
            bool currCorrectEnd = curr.OpCode == OpCodes.Callvirt 
                && ((MethodReference)curr.Operand).FullName == "System.Void InputHandler::SetSkipMode(GlobalEnums.SkipPromptMode)";
            bool nextCorrectEnd = next.OpCode == OpCodes.Ldarg_0;


            if (prevCorrectStart&& currCorrectStart && nextCorrectStart) {
                startIdx = idx - 1;
            } else if (prevCorrectEnd && currCorrectEnd && nextCorrectEnd) {
                endIdx = idx + 1;
            }
        }

        for (int idx = 0; idx < endIdx - startIdx; idx++)
        {
            il.RemoveAt(startIdx);
        }

        FieldDefinition chainSequenceField = _targetType.Fields.First(field => field.Name == "chainSequence");

        TypeDefinition chainSequenceType = _targetModule.GetType("ChainSequence");
        MethodDefinition isCurrentSkipped = chainSequenceType.Methods.First(method => method.Name == "get_IsCurrentSkipped");
        MethodDefinition skipSingle = chainSequenceType.Methods.First(method => method.Name == "SkipSingle");


        Instruction insertBefore = il.Body.Instructions[startIdx];
        il.InsertBefore(insertBefore, il.Create(OpCodes.Ldloc_1));
        il.InsertBefore(insertBefore, il.Create(OpCodes.Ldfld, chainSequenceField));
        il.InsertBefore(insertBefore, il.Create(OpCodes.Callvirt, isCurrentSkipped));
        il.InsertBefore(insertBefore, il.Create(OpCodes.Brtrue_S, insertBefore));
        il.InsertBefore(insertBefore, il.Create(OpCodes.Ldloc_1));
        il.InsertBefore(insertBefore, il.Create(OpCodes.Ldfld, chainSequenceField));
        il.InsertBefore(insertBefore, il.Create(OpCodes.Callvirt, skipSingle));
    }
}
