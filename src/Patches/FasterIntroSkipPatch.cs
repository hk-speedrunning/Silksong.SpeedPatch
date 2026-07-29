using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SilksongDoorstop;

namespace SilksongDoorstop.Patches;

internal class FasterIntroSkipPatch : Patch
{
    private ModuleDefinition _targetModule;
    private TypeDefinition _targetType;
    private MethodDefinition _targetMethod;

    public FasterIntroSkipPatch(ModuleDefinition targetModule)
    {
        _targetModule = targetModule;
        _targetType = _targetModule.GetType("OpeningSequence");

        _targetMethod = _targetType.Methods.First(method => method.Name == "Update");
    }

    public void ApplyPatch()
    {
        ILProcessor il = _targetMethod.Body.GetILProcessor();

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

        int idx = 0;
        il.InsertBefore(il.Body.Instructions[idx], il.Create(OpCodes.Call, getInstance));
        il.InsertAfter(idx++, il.Create(OpCodes.Callvirt, getInputHandler));
        il.InsertAfter(idx++, il.Create(OpCodes.Ldc_I4, enumVal));
        il.InsertAfter(idx++, il.Create(OpCodes.Callvirt, setSkipMode));
    }
}
