using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SilksongDoorstop.Patches;

internal abstract class CopyPatch : Patch
{
    protected readonly ModuleDefinition TargetModule;

    protected readonly MethodDefinition TargetMethod;
    protected readonly MethodDefinition SourceMethod;

    protected CopyPatch(ModuleDefinition targetModule, ModuleDefinition sourceModule, string typeName, string methodName)
    {
        TypeDefinition sourceType = sourceModule.GetType($"SilksongDoorstop.Patches.{typeName}");
        SourceMethod = sourceType.Methods.First(method => method.Name == methodName);

        TargetModule = targetModule;
        TypeDefinition targetType = TargetModule.GetType(typeName);
        try
        {
            TargetMethod = targetType.Methods.First(method =>
                method.Name == methodName &&
                method.Parameters.Count == SourceMethod.Parameters.Count &&
                method.Parameters.SequenceEqual(SourceMethod.Parameters)
            );
        }
        catch (Exception)
        {
            TargetMethod = new MethodDefinition(methodName, SourceMethod.Attributes, targetModule.ImportReference(SourceMethod.ReturnType));
            targetType.Methods.Add(TargetMethod);
        }
    }

    public virtual void ApplyPatch()
    {
        CopyParameters();
        CopyLocals();
        CopyCode();
    }

    private void CopyParameters()
    {
        foreach (ParameterDefinition paramDef in SourceMethod.Parameters)
        {
            paramDef.ParameterType = TargetModule.ImportReference(paramDef.ParameterType);
            TargetMethod.Parameters.Add(paramDef);
        }
    }

    protected void CopyLocals()
    {
        TargetMethod.Body.InitLocals = SourceMethod.Body.InitLocals;

        foreach (VariableDefinition varDef in SourceMethod.Body.Variables)
        {
            varDef.VariableType = TargetModule.ImportReference(varDef.VariableType);
            TargetMethod.Body.Variables.Add(varDef);
        }
    }

    private void CopyCode()
    {
        ILProcessor il = TargetMethod.Body.GetILProcessor();

        foreach (Instruction inst in SourceMethod.Body.Instructions)
        {
            Instruction newInst = CheckInstruction(il, inst);
            il.Append(newInst);
        }
    }

    protected void InsertCode(int insertIndex, int localsOffset)
    {
        ILProcessor il = TargetMethod.Body.GetILProcessor();

        foreach (Instruction inst in SourceMethod.Body.Instructions)
        {
            Instruction newInst = CheckInstruction(il, inst, true, localsOffset);

            il.InsertAfter(insertIndex++, newInst);
        }
    }

    private Instruction CheckInstruction(ILProcessor il, Instruction inst, bool reassignLocals = false, int localsOffset = 0)
    {
        if (inst.OpCode.FlowControl == FlowControl.Call)
        {
            inst.Operand = TargetModule.ImportReference((MethodReference)inst.Operand);
        }
        else if (inst.OpCode.OperandType == OperandType.InlineField)
        {
            inst.Operand = TargetModule.ImportReference((FieldReference)inst.Operand);
        }
        else if (reassignLocals && IsLocalOperand(inst.OpCode))
        {
            VariableDefinition local = GetLocal(inst);

            int localIndex = 0;
            for (localIndex = 0; localIndex < TargetMethod.Body.Variables.Count; localIndex++)
            {
                if (TargetMethod.Body.Variables[localIndex] == local)
                {
                    break;
                }
            }

            localIndex += localsOffset;

            if (localIndex < TargetMethod.Body.Variables.Count)
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

    private static bool IsLocalOperand(OpCode op)
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

    private VariableDefinition GetLocal(Instruction inst)
    {
        if (inst.OpCode == OpCodes.Ldloc_0 || inst.OpCode == OpCodes.Stloc_0)
        {
            return TargetMethod.Body.Variables[0];
        }
        else if (inst.OpCode == OpCodes.Ldloc_1 || inst.OpCode == OpCodes.Stloc_1)
        {
            return TargetMethod.Body.Variables[1];
        }
        else if (inst.OpCode == OpCodes.Ldloc_2 || inst.OpCode == OpCodes.Stloc_2)
        {
            return TargetMethod.Body.Variables[2];
        }
        else if (inst.OpCode == OpCodes.Ldloc_3 || inst.OpCode == OpCodes.Stloc_3)
        {
            return TargetMethod.Body.Variables[3];
        }
        return (VariableDefinition)inst.Operand;
    }
}
