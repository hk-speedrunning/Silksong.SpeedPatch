using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SilksongDoorstop;

namespace SilksongDoorstop.Patches;

internal class FasterIntroSkipPatch : Patch
{
    private ModuleDefinition _targetModule;
    private TypeDefinition _syncType;
    private TypeDefinition _asyncType;

    public FasterIntroSkipPatch(ModuleDefinition targetModule)
    {
        _targetModule = targetModule;
        TypeDefinition targetType = _targetModule.GetType("OpeningSequence");

        _syncType = targetType.NestedTypes.First(type => type.Name.StartsWith("<StartSync>d__"));
        _asyncType = targetType.NestedTypes.First(type => type.Name.StartsWith("<StartAsync>d__"));
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

        TypeDefinition gameManagerType = _targetModule.GetType("GameManager");
        MethodDefinition getInstance = gameManagerType.Methods.First(method => method.Name == "get_instance");
        MethodDefinition getInputHandler = gameManagerType.Methods.First(method => method.Name == "get_inputHandler");

        TypeDefinition inputHandlerType = _targetModule.GetType("InputHandler");
        MethodDefinition setSkipMode = inputHandlerType.Methods.First(method => method.Name == "SetSkipMode");

        TypeDefinition skipPromptModeEnumType = _targetModule.GetType("GlobalEnums.SkipPromptMode");

        int enumVal = 0;
        for (int enumIdx = 0; enumIdx < skipPromptModeEnumType.Fields.Count; enumIdx++)
        {
            FieldDefinition field = skipPromptModeEnumType.Fields[enumIdx];
            if (field.Name == "SKIP_INSTANT") {
                enumVal = (int)field.Constant;
                break;
            }
        }

        int inserIdx = startIdx;
        il.InsertBefore(il.Body.Instructions[inserIdx], il.Create(OpCodes.Call, getInstance));
        il.InsertAfter(inserIdx++, il.Create(OpCodes.Callvirt, getInputHandler));
        il.InsertAfter(inserIdx++, il.Create(OpCodes.Ldc_I4, enumVal));
        il.InsertAfter(inserIdx++, il.Create(OpCodes.Callvirt, setSkipMode));
    }
}
