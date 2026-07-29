using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SilksongDoorstop;

namespace SilksongDoorstop.Patches;

internal class TextMasherPatch : Patch
{
    private ModuleDefinition _targetModule;
    private TypeDefinition _targetType;
    private MethodDefinition _fixedUpdateMethod;
    private MethodDefinition _isActiveMethod;

    public TextMasherPatch(ModuleDefinition targetModule)
    {
        _targetModule = targetModule;
        _targetType = _targetModule.GetType("DialogueBox");

        _targetType.Methods.Remove(_targetType.Methods.First(method => method.Name == "Update"));
        _fixedUpdateMethod = new("FixedUpdate", MethodAttributes.Private, _targetModule.TypeSystem.Void);
        _targetType.Methods.Add(_fixedUpdateMethod);

        _isActiveMethod = new("IsActive", MethodAttributes.Private, _targetModule.TypeSystem.Boolean);
        _targetType.Methods.Add(_isActiveMethod);
    }

    public void ApplyPatch()
    {
        ILProcessor il = _fixedUpdateMethod.Body.GetILProcessor();

        FieldReference waitingToAdvance = _targetType.Fields.First(field => field.Name == "waitingToAdvance");
        MethodReference advanceConversation = _targetType.Methods.First(method => method.Name == "AdvanceConversation");

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Callvirt, _isActiveMethod);
        Instruction ret = il.Create(OpCodes.Ret);
        il.Emit(OpCodes.Brfalse_S, ret);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldc_I4_1);
        il.Emit(OpCodes.Stfld, waitingToAdvance);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Callvirt, advanceConversation);
        il.Append(ret);

        TypeDefinition heroActions = _targetModule.GetType("HeroActions");

        VariableDefinition actions = new(heroActions);
        _isActiveMethod.Body.Variables.Add(actions);
        _isActiveMethod.Body.InitLocals = true;

        FieldDefinition isDialogueRunning = _targetType.Fields.First(field => field.Name == "isDialogueRunning");
        FieldDefinition conversationEnded= _targetType.Fields.First(field => field.Name == "conversationEnded");

        TypeDefinition gameManager = _targetModule.GetType("GameManager");
        MethodDefinition getInstance = gameManager.Methods.First(method => method.Name == "get_instance");
        MethodDefinition getInputHandler = gameManager.Methods.First(method => method.Name == "get_inputHandler");

        TypeDefinition inputHandler = _targetModule.GetType("InputHandler");
        FieldDefinition inputActions = inputHandler.Fields.First(field => field.Name == "inputActions");

        FieldDefinition attackAction = heroActions.Fields.First(field => field.Name == "Attack");
        FieldDefinition jumpAction = heroActions.Fields.First(field => field.Name == "Jump");
        FieldDefinition dashAction = heroActions.Fields.First(field => field.Name == "Dash");
        FieldDefinition castAction = heroActions.Fields.First(field => field.Name == "Cast");
        FieldDefinition quickCastAction = heroActions.Fields.First(field => field.Name == "QuickCast");

        TypeDefinition oneAxisInputControl= _targetModule.GetType("InControl.OneAxisInputControl");
        MethodDefinition getIsPressed = oneAxisInputControl.Methods.First(method => method.Name == "get_IsPressed");

        il = _isActiveMethod.Body.GetILProcessor();

        // HeroActions actions = GameManager.instance.inputHandler.inputActions;
        il.Emit(OpCodes.Call, getInstance);
        il.Emit(OpCodes.Callvirt, getInputHandler);
        il.Emit(OpCodes.Ldfld, inputActions);
        il.Emit(OpCodes.Stloc, actions);

        // isDialogueRunning && inputActions.Attack.IsPressed && inputActions.Jump.IsPressed && 
        // inputActions.Jump.IsP essed && inputActions.Cast.IsPressed && inputActions.QuickCast.IsPressed;
        il.Emit(OpCodes.Ldloc, actions);
        il.Emit(OpCodes.Ldfld, attackAction);
        il.Emit(OpCodes.Callvirt, getIsPressed);
        il.Emit(OpCodes.Ldloc, actions);
        il.Emit(OpCodes.Ldfld, jumpAction);
        il.Emit(OpCodes.Callvirt, getIsPressed);
        il.Emit(OpCodes.Or);
        il.Emit(OpCodes.Ldloc, actions);
        il.Emit(OpCodes.Ldfld, dashAction);
        il.Emit(OpCodes.Callvirt, getIsPressed);
        il.Emit(OpCodes.Or);
        il.Emit(OpCodes.Ldloc, actions);
        il.Emit(OpCodes.Ldfld, castAction);
        il.Emit(OpCodes.Callvirt, getIsPressed);
        il.Emit(OpCodes.Or);
        il.Emit(OpCodes.Ldloc, actions);
        il.Emit(OpCodes.Ldfld, quickCastAction);
        il.Emit(OpCodes.Callvirt, getIsPressed);
        il.Emit(OpCodes.Or);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, isDialogueRunning);
        il.Emit(OpCodes.And);
        il.Emit(OpCodes.Ret);
    }
}
