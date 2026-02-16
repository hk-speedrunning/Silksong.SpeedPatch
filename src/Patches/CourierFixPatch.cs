using System;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SilksongDoorstop.Patches;

internal class CourierFixPatch : CopyPatch
{

    public CourierFixPatch(ModuleDefinition targetModule, ModuleDefinition sourceModule)
        : base(targetModule, sourceModule, "SimpleQuestsShopOwner", "GetItems") { }

    override public void ApplyPatch()
    {
        int varCount = _targetMethod.Body.Variables.Count;


        int insertIndex = 0;
        foreach (Instruction inst in _targetMethod.Body.Instructions)
        {
            if (inst.OpCode == OpCodes.Call && ((MethodReference)inst.Operand).FullName.Contains("Extensions::Shuffle"))
            {
                break;
            }
            insertIndex++;
        }

        CopyLocals();
        InsertCode(insertIndex, varCount);

        _targetMethod.Body.GetILProcessor()
            .Remove(_targetMethod.Body.Instructions.First(inst =>
                inst.OpCode == OpCodes.Ret
            )
        );

        ILProcessor il = _targetMethod.Body.GetILProcessor();

        for (int i = 0; i < _sourceMethod.Body.Instructions.Count - 1; i++)
        {
            Instruction inst = il.Body.Instructions[insertIndex + 1 + i];
            if (inst.OpCode.OperandType == OperandType.InlineField &&
                ((FieldReference)inst.Operand).Name == "runningGenericList")
            {
                FieldDefinition runningGenericField = _targetModule.GetType("SimpleQuestsShopOwner").Fields.First(field => field.Name == "runningGenericList");
                il.Replace(inst, il.Create(inst.OpCode, _targetModule.ImportReference(runningGenericField)));
            }
        }
    }
}

/* MIT License
 *
 * Copyright (c) 2026 spacemonkeyy
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */

internal class SimpleQuestsShopOwner : global::SimpleQuestsShopOwner
{
    private List<ShopItemInfo> runningGenericList = new();

    new private void GetItems()
    {
        List<ShopItemInfo> completed = new();
        List<ShopItemInfo> notCompleted = new();

        for (int i = 0; i < runningGenericList.Count; i++)
        {
            if (runningGenericList[i].Quest.IsCompleted)
            {
                completed.Add(runningGenericList[i]);
            }
            else
            {
                notCompleted.Add(runningGenericList[i]);
            }
        }

        notCompleted.AddRange(completed);
        runningGenericList = notCompleted;
    }
}
