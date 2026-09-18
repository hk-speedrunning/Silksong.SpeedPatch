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
    private MethodDefinition _lineEndedWaitMethod;

    private MethodReference _invoke;

    public TextMasherPatch(ModuleDefinition targetModule)
    {
        _targetModule = targetModule;
        _targetType = _targetModule.GetType("DialogueBox");

        TypeDefinition monoBehaviour = _targetType.BaseType.Resolve();
        _invoke = _targetModule.ImportReference(monoBehaviour.Methods.First(method => method.Name == "Invoke"));

        TypeDefinition coroutineType = _targetType.NestedTypes.First(type => type.Name.StartsWith("<LineEndedWait>d__"));
        _lineEndedWaitMethod = coroutineType.Methods.First(method => method.Name == "MoveNext");

        _fixedUpdateMethod = new("FixedUpdate", MethodAttributes.Private, _targetModule.TypeSystem.Void);
        _isActiveMethod = new("IsActive", MethodAttributes.Private, _targetModule.TypeSystem.Boolean);
    }

    public void ApplyPatch()
    {
        _targetType.Methods.Remove(_targetType.Methods.First(method => method.Name == "Update"));
        _targetType.Methods.Add(_fixedUpdateMethod);
        _targetType.Methods.Add(_isActiveMethod);

        PatchLineEndedWait();

        CreateFixedUpdate();

        CreateIsActive();
    }

    private void PatchLineEndedWait()
    {
        ILProcessor il = _lineEndedWaitMethod.Body.GetILProcessor();

        FieldReference waitingToAdvance = _targetType.Fields.First(field => field.Name == "waitingToAdvance");

        int i = 1;
        for (; i < il.Body.Instructions.Count-1; i++)
        {
            Instruction prevInst = il.Body.Instructions[i-1];
            Instruction currInst = il.Body.Instructions[i];
            Instruction nextInst = il.Body.Instructions[i+1];
            if (prevInst.OpCode == OpCodes.Ldarg_0 && currInst.OpCode == OpCodes.Ldnull && nextInst.OpCode == OpCodes.Stfld)
            {
                i--;
                break;
            }
        }

        Instruction old_branch_target = il.Body.Instructions[i];

        Instruction ld_this = il.Create(OpCodes.Ldloc_1);

        il.InsertBefore(old_branch_target, ld_this);
        il.InsertAfter(i++, il.Create(OpCodes.Ldstr, "AdvanceConversation"));
        il.InsertAfter(i++, il.Create(OpCodes.Ldc_R4, 0f));
        il.InsertAfter(i++, il.Create(OpCodes.Callvirt, _invoke));

        for (; i < il.Body.Instructions.Count-1; i++)
        {
            Instruction prevInst = il.Body.Instructions[i-1];
            Instruction currInst = il.Body.Instructions[i];
            Instruction nextInst = il.Body.Instructions[i+1];
            if (prevInst.OpCode == OpCodes.Ldloc_1 && currInst.OpCode == OpCodes.Ldfld && nextInst.OpCode == OpCodes.Brfalse_S && ((Instruction)nextInst.Operand) == old_branch_target)
            {
                i++;
                break;
            }
        }

        il.Replace(i, il.Create(OpCodes.Brfalse_S, ld_this));
    }

    private void CreateFixedUpdate()
    {
        ILProcessor il = _fixedUpdateMethod.Body.GetILProcessor();

        FieldReference currentRevealSpeed = _targetType.Fields.First(field => field.Name == "currentRevealSpeed");
        FieldReference fastRevealSpeed = _targetType.Fields.First(field => field.Name == "fastRevealSpeed");
        MethodReference advanceConversation = _targetType.Methods.First(method => method.Name == "AdvanceConversation");

        Instruction ret = il.Create(OpCodes.Ret);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Callvirt, _isActiveMethod);
        il.Emit(OpCodes.Brfalse_S, ret);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, currentRevealSpeed);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, fastRevealSpeed);
        il.Emit(OpCodes.Bge_S, ret);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldstr, "AdvanceConversation");
        il.Emit(OpCodes.Ldc_R4, 0f);
        il.Emit(OpCodes.Callvirt, _invoke);
        il.Append(ret);
    }

    private void CreateIsActive()
    {
        ILProcessor il = _isActiveMethod.Body.GetILProcessor();

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

        // HeroActions actions = GameManager.instance.inputHandler.inputActions;
        il.Emit(OpCodes.Call, getInstance);
        il.Emit(OpCodes.Callvirt, getInputHandler);
        il.Emit(OpCodes.Ldfld, inputActions);
        il.Emit(OpCodes.Stloc, actions);

        // isDialogueRunning && (inputActions.Attack.IsPressed || inputActions.Jump.IsPressed || 
        // inputActions.Jump.IsPressed || inputActions.Cast.IsPressed || inputActions.QuickCast.IsPressed)
        Instruction ld_false = il.Create(OpCodes.Ldc_I4_0);
        Instruction ld_true = il.Create(OpCodes.Ldc_I4_1);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, isDialogueRunning);
        il.Emit(OpCodes.Brfalse_S, ld_false);

        il.Emit(OpCodes.Ldloc, actions);
        il.Emit(OpCodes.Ldfld, attackAction);
        il.Emit(OpCodes.Callvirt, getIsPressed);
        il.Emit(OpCodes.Brtrue_S, ld_true);

        il.Emit(OpCodes.Ldloc, actions);
        il.Emit(OpCodes.Ldfld, jumpAction);
        il.Emit(OpCodes.Callvirt, getIsPressed);
        il.Emit(OpCodes.Brtrue_S, ld_true);

        il.Emit(OpCodes.Ldloc, actions);
        il.Emit(OpCodes.Ldfld, dashAction);
        il.Emit(OpCodes.Callvirt, getIsPressed);
        il.Emit(OpCodes.Brtrue_S, ld_true);

        il.Emit(OpCodes.Ldloc, actions);
        il.Emit(OpCodes.Ldfld, castAction);
        il.Emit(OpCodes.Callvirt, getIsPressed);
        il.Emit(OpCodes.Brtrue_S, ld_true);

        il.Emit(OpCodes.Ldloc, actions);
        il.Emit(OpCodes.Ldfld, quickCastAction);
        il.Emit(OpCodes.Callvirt, getIsPressed);
        il.Emit(OpCodes.Brtrue_S, ld_true);

        il.Append(ld_false);
        il.Emit(OpCodes.Ret);

        il.Append(ld_true);
        il.Emit(OpCodes.Ret);
    }
}
