using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SilksongDoorstop.Patches;

internal abstract class CopyPatch : Patch
{
    protected ModuleDefinition _targetModule;
    protected ModuleDefinition _sourceModule;

    protected MethodDefinition _targetMethod;
    protected MethodDefinition _sourceMethod;

    public CopyPatch(ModuleDefinition targetModule, ModuleDefinition sourceModule, string typeName, string methodName)
    {
        _sourceModule = sourceModule;
        TypeDefinition sourceType = _sourceModule.GetType($"SilksongDoorstop.Patches.{typeName}");
        _sourceMethod = sourceType.Methods.First(method => method.Name == methodName);

        _targetModule = targetModule;
        TypeDefinition targetType = _targetModule.GetType(typeName);
        try
        {
            _targetMethod = targetType.Methods.First(method =>
                method.Name == methodName &&
                method.Parameters.Count == _sourceMethod.Parameters.Count &&
                method.Parameters.SequenceEqual(_sourceMethod.Parameters)
            );
        }
        catch (Exception)
        {
            _targetMethod = new MethodDefinition(methodName, _sourceMethod.Attributes, targetModule.ImportReference(_sourceMethod.ReturnType));
            targetType.Methods.Add(_targetMethod);
        }
    }

    virtual public void ApplyPatch()
    {
        CopyParameters();
        CopyLocals();
        CopyCode();
    }

    protected void CopyParameters()
    {
        foreach (ParameterDefinition paramDef in _sourceMethod.Parameters)
        {
            paramDef.ParameterType = _targetModule.ImportReference(paramDef.ParameterType);
            _targetMethod.Parameters.Add(paramDef);
        }
    }

    protected void CopyLocals()
    {
        _targetMethod.Body.InitLocals = _sourceMethod.Body.InitLocals;

        foreach (VariableDefinition varDef in _sourceMethod.Body.Variables)
        {
            varDef.VariableType = _targetModule.ImportReference(varDef.VariableType);
            _targetMethod.Body.Variables.Add(varDef);
        }
    }

    protected void CopyCode()
    {
        ILProcessor il = _targetMethod.Body.GetILProcessor();

        foreach (Instruction inst in _sourceMethod.Body.Instructions)
        {
            Instruction newInst = CheckInstruction(il, inst);
            il.Append(newInst);
        }
    }

    protected void InsertCode(int insertIndex, int localsOffset)
    {
        ILProcessor il = _targetMethod.Body.GetILProcessor();

        foreach (Instruction inst in _sourceMethod.Body.Instructions)
        {
            Instruction newInst = CheckInstruction(il, inst, true, localsOffset);

            il.InsertAfter(insertIndex++, newInst);
        }
    }

    private Instruction CheckInstruction(ILProcessor il, Instruction inst, bool reassignLocals = false, int localsOffset = 0)
    {
        if (inst.OpCode.FlowControl == FlowControl.Call)
        {
            inst.Operand = _targetModule.ImportReference((MethodReference)inst.Operand);
        }
        else if (inst.OpCode.OperandType == OperandType.InlineField)
        {
            inst.Operand = _targetModule.ImportReference((FieldReference)inst.Operand);
        }
        else if (reassignLocals && isLocalOperand(inst.OpCode))
        {
            VariableDefinition local = getLocal(inst);

            int localIndex = 0;
            for (localIndex = 0; localIndex < _targetMethod.Body.Variables.Count; localIndex++)
            {
                if (_targetMethod.Body.Variables[localIndex] == local)
                {
                    break;
                }
            }

            localIndex += localsOffset;

            if (localIndex < _targetMethod.Body.Variables.Count)
            {
                if (inst.OpCode.StackBehaviourPop == StackBehaviour.Pop0)
                {
                    Instruction newInst = il.Create(OpCodes.Ldloc, localIndex);
                    inst.OpCode = newInst.OpCode;
                    inst.Operand = newInst.Operand;
                }
                else
                {
                    Instruction newInst = il.Create(OpCodes.Stloc, localIndex);
                    inst.OpCode = newInst.OpCode;
                    inst.Operand = newInst.Operand;
                }
            }
        }

        return inst;
    }

    private static bool isLocalOperand(OpCode op)
    {
        return op.OperandType == OperandType.ShortInlineVar ||
                op.OperandType == OperandType.InlineVar ||
                op == OpCodes.Ldloc_0 ||
                op == OpCodes.Ldloc_1 ||
                op == OpCodes.Ldloc_2 ||
                op == OpCodes.Ldloc_3 ||

                op == OpCodes.Stloc_0 ||
                op == OpCodes.Stloc_1 ||
                op == OpCodes.Stloc_2 ||
                op == OpCodes.Stloc_3
            ;

    }

    private VariableDefinition getLocal(Instruction inst)
    {
        if (inst.OpCode == OpCodes.Ldloc_0 || inst.OpCode == OpCodes.Stloc_0)
        {
            return _targetMethod.Body.Variables[0];
        }
        else if (inst.OpCode == OpCodes.Ldloc_1 || inst.OpCode == OpCodes.Stloc_1)
        {
            return _targetMethod.Body.Variables[1];
        }
        else if (inst.OpCode == OpCodes.Ldloc_2 || inst.OpCode == OpCodes.Stloc_2)
        {
            return _targetMethod.Body.Variables[2];
        }
        else if (inst.OpCode == OpCodes.Ldloc_3 || inst.OpCode == OpCodes.Stloc_3)
        {
            return _targetMethod.Body.Variables[3];
        }
        return (VariableDefinition)inst.Operand;
    }
}
