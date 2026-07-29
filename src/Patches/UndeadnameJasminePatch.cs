using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SilksongDoorstop;

namespace SilksongDoorstop.Patches;

internal class UndeadnameJasminePatch : Patch
{
    private ModuleDefinition _targetModule;

    protected MethodDefinition _targetMethod;

    public UndeadnameJasminePatch(ModuleDefinition targetModule)
    {
        _targetModule = targetModule;
        TypeDefinition targetType = _targetModule.GetType("TeamCherry.Localization.Language");

        _targetMethod = targetType.Methods.First(method => method.Name == "Get" && method.Parameters.Count == 2);
    }

    public void ApplyPatch()
    {
        ILProcessor il = _targetMethod.Body.GetILProcessor();

        VariableDefinition localTextVar = new VariableDefinition(_targetModule.TypeSystem.String);

        il.Body.Variables.Add(localTextVar);
        il.Body.InitLocals = true;

        int i = 1;
        for (; i < il.Body.Instructions.Count-1; i++)
        {
            Instruction prevInst = il.Body.Instructions[i-1];
            Instruction currInst = il.Body.Instructions[i];
            Instruction nextInst = il.Body.Instructions[i+1];
            if (prevInst.OpCode == OpCodes.Ldarg_0 && currInst.OpCode == OpCodes.Callvirt && nextInst.OpCode == OpCodes.Ret)
            {
                break;
            }
        }

        { // store
            il.InsertAfter(i++, il.Create(OpCodes.Stloc_0));
            il.InsertAfter(i, il.Create(OpCodes.Ldloc_0));
        }

        { // compare


            il.InsertAfter(i++, il.Create(OpCodes.Ldloc_0));
            il.InsertAfter(i++, il.Create(OpCodes.Ldstr, "Jack Vine"));

            MethodReference opEqualityRef = new MethodReference("op_Equality", _targetModule.TypeSystem.Boolean, _targetModule.TypeSystem.String){
                HasThis = false,
                CallingConvention = MethodCallingConvention.Default
            };
            opEqualityRef.Parameters.Add(new ParameterDefinition(_targetModule.TypeSystem.String));
            opEqualityRef.Parameters.Add(new ParameterDefinition(_targetModule.TypeSystem.String));

            il.InsertAfter(i++, il.Create(OpCodes.Call, _targetModule.ImportReference(opEqualityRef)));

            il.InsertAfter(i++, il.Create(OpCodes.Brfalse_S, il.Body.Instructions[i]));
        }

        { // replace
            il.InsertAfter(i++, il.Create(OpCodes.Ldstr, "Jasmine Vine"));
            il.InsertAfter(i++, il.Create(OpCodes.Stloc_0));
        }
    }
}
