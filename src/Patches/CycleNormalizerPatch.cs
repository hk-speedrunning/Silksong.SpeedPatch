using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using MonoMod.Cil;
using UnityEngine;
using UnityEngine.SceneManagement;
using FieldAttributes = Mono.Cecil.FieldAttributes;

namespace SilksongDoorstop.Patches;

internal class CycleNormalizerPatch : Patch
{
    private ModuleDefinition _targetModule;

    public CycleNormalizerPatch(ModuleDefinition targetModule)
    {
        _targetModule = targetModule;
    }

    public void ApplyPatch()
    {
        TypeDefinition sceneLoadType = _targetModule.GetType(nameof(SceneLoad));
        TypeDefinition stateMachineType = sceneLoadType.NestedTypes.First(type => type.Name.StartsWith("<BeginRoutine>d__"));
        MethodDefinition targetMethod = stateMachineType.Methods.First(method => method.Name == "MoveNext");

        FieldDefinition stateField = stateMachineType.Fields.First(field => field.Name.Contains("__state"));
        FieldDefinition currentField = stateMachineType.Fields.First(field => field.Name.Contains("__current"));

        // Add a new field to SceneLoad of type TimeManager.TimeControlInstance, named _timeControl
        TypeDefinition timeControlInstanceType = _targetModule.GetType(nameof(TimeManager))
            .NestedTypes.First(type => type.Name.Contains(nameof(TimeManager.TimeControlInstance)));
        FieldDefinition timeControlField = new("_timeControl", FieldAttributes.Private, timeControlInstanceType);
        sceneLoadType.Fields.Add(timeControlField);

        using ILContext ilContext = new(targetMethod);
        ILCursor il = new(ilContext);

        il.Goto(0);
        il.GotoNext(i => i.Match(OpCodes.Switch));
        Instruction switchInstruction = il.Next!;
        List<ILLabel> switchTargets = (switchInstruction.Operand as Instruction[])!.Select(il.MarkLabel).ToList();

        il.GotoNext(i => i.Match(OpCodes.Leave));
        ILLabel returnLabel = il.MarkLabel((il.Next!.Operand as Instruction)!);

        /*
         * RecordBeginTime(Phases.Activation);
         * <-- BEGIN INJECTED -->
         * bool freezeTime = SceneManager.loadedSceneCount - SceneAdditiveLoadConditional._additiveSceneLoads.Count <= 1
         *     | GameManager.instance.RespawningHero;
         * if (freezeTime)
         * {
         *     _timeControl = new TimeManager.TimeControlInstance(0f, TimeManager.TimeControlInstance.Type.Multiplicative);
         * }
         * <-- END INJECTED -->
         */

        il.Goto(0);
        il.GotoNext(MoveType.After,
            i => i.MatchLdloc(2),
            i => i.MatchLdcI4(4),
            i => i.MatchCall(sceneLoadType.Methods.First(method => method.Name == nameof(SceneLoad.RecordBeginTime)))
        );

        // SceneManager.loadedSceneCount - SceneAdditiveLoadConditional._additiveSceneLoads.Count <= 1
        il.EmitCall(_targetModule.ImportReference(typeof(SceneManager)
            .GetProperty(nameof(SceneManager.loadedSceneCount))!.GetMethod));
        FieldDefinition additiveSceneLoadsField = _targetModule.GetType(nameof(SceneAdditiveLoadConditional))
            .Fields.First(field => field.Name == nameof(SceneAdditiveLoadConditional._additiveSceneLoads));
        additiveSceneLoadsField.IsPublic = true;
        il.EmitLdsfld(additiveSceneLoadsField);
        // NB: this type CANNOT be List<SceneAdditiveLoadConditional>. If that is used,
        // Mono attempts to load Assembly-CSharp.dll before running this patch, which breaks everything.
        // Any List<T> works fine, Mono has no problem finding the correct property at runtime.
        il.EmitCall(_targetModule.ImportReference(typeof(List<int>)
            .GetProperty(nameof(List<>.Count))!.GetMethod));
        il.EmitSub();
        il.EmitLdcI4(2);
        il.EmitClt();

        // GameManager.instance.RespawningHero
        il.EmitCall(_targetModule.GetType(nameof(GameManager))
            .Properties.First(property => property.Name == nameof(global::GameManager.instance)).GetMethod);
        il.EmitCall(_targetModule.GetType(nameof(GameManager))
            .Properties.First(property => property.Name == nameof(global::GameManager.RespawningHero)).GetMethod);

        // bool freezeTime = [...] | [...];
        il.EmitOr();
        VariableDefinition freezeTimeLocal = il.CreateLocal(typeof(bool));
        il.EmitStloc(freezeTimeLocal);

        // if (freezeTime) { ... }
        ILLabel afterSetTimeControlLabel = il.DefineLabel();
        il.EmitLdloc(freezeTimeLocal);
        il.EmitBrfalse(afterSetTimeControlLabel);
        il.MarkLabel(afterSetTimeControlLabel);
        il.MoveBeforeLabels();

        // _timeControl = new TimeManager.TimeControlInstance(0f, TimeManager.TimeControlInstance.Type.Multiplicative);
        il.EmitLdloc(2);
        il.EmitLdcR4(0f);
        il.EmitLdcI4(0);
        il.EmitNewobj(_targetModule.GetType(nameof(TimeManager))
            .NestedTypes.First(type => type.Name.Contains(nameof(TimeManager.TimeControlInstance)))
            .GetConstructors().First());
        il.EmitStfld(timeControlField);

        /*
         * RecordBeginTime(SceneLoad.Phases.StartCall);
         * yield return null;
         * <-- BEGIN INJECTED -->
         * if (freezeTime)
         * {
         *     yield return null;
         * }
         * <-- END INJECTED -->
         * RecordEndTime(SceneLoad.Phases.StartCall);
         */

        il.Goto(0);
        il.GotoNext(MoveType.AfterLabel,
            i => i.MatchLdarg(0),
            i => i.MatchLdcI4(-1),
            i => i.MatchStfld(stateField),
            i => i.MatchLdloc(2),
            i => i.MatchLdcI4(7),
            i => i.MatchCall(sceneLoadType.Methods.First(method => method.Name == nameof(SceneLoad.RecordEndTime)))
        );

        // if (freezeTime) { ... }
        ILLabel afterSecondYieldLabel = il.DefineLabel();
        il.EmitLdloc(freezeTimeLocal);
        il.EmitBrfalse(afterSecondYieldLabel);

        // yield return null;
        il.MarkLabel(afterSecondYieldLabel);
        il.MoveBeforeLabels();
        int afterSecondYieldState = switchTargets.Count;
        switchTargets.Add(afterSecondYieldLabel);
        il.EmitLdarg(0);
        il.EmitLdnull();
        il.EmitStfld(currentField);
        il.EmitLdarg(0);
        il.EmitLdcI4(afterSecondYieldState);
        il.EmitStfld(stateField);
        il.EmitLdcI4(1);
        il.EmitStloc(0);
        il.EmitLeave(returnLabel);

        /*
         * ...
         * <-- BEGIN INJECTED -->
         * if (_timeControl != null)
         * {
         *     _timeControl.Release();
         * }
         * yield return new WaitForSecondsRealtime(0.06f);
         * <-- END INJECTED -->
         * this.IsFinished = true;
         */

        il.Goto(0);
        il.GotoNext(MoveType.AfterLabel,
            i => i.MatchLdloc(2),
            i => i.MatchLdcI4(1),
            i => i.MatchCall(sceneLoadType.Properties.First(property => property.Name == nameof(SceneLoad.IsFinished)).SetMethod)
        );

        // Replace the try/catch branch target with a nop, then add our code underneath.
        // The real instruction is copied to the end.
        OpCode targetOpCode = il.Next.OpCode;
        object targetOperand = il.Next.Operand;
        il.Next.OpCode = OpCodes.Nop;
        il.Next.Operand = null;
        il.GotoNext();
        il.EmitNop();
        il.Prev.OpCode = targetOpCode;
        il.Prev.Operand = targetOperand;
        il.GotoPrev();

        // if (_timeControl != null) { ... }
        ILLabel afterReleaseTimeControlLabel = il.DefineLabel();
        il.EmitLdloc(2);
        il.EmitLdfld(timeControlField);
        il.EmitLdnull();
        il.EmitBeq(afterReleaseTimeControlLabel);

        // _timeControl.Release();
        il.EmitLdloc(2);
        il.EmitLdfld(timeControlField);
        il.EmitCall(timeControlInstanceType.Methods.First(method => method.Name == nameof(TimeManager.TimeControlInstance.Release)));

        il.EmitNop();
        il.GotoPrev();
        il.MarkLabel(afterReleaseTimeControlLabel);
        il.GotoNext();

        // yield return new WaitForSecondsRealtime(0.06f);
        ILLabel afterYieldDelayLabel = il.MarkLabel();
        il.MoveBeforeLabels();
        int afterYieldDelayState = switchTargets.Count;
        switchTargets.Add(afterYieldDelayLabel);
        il.EmitLdarg(0);
        il.EmitLdcR4(0.06f);
        il.EmitNewobj(_targetModule.ImportReference(typeof(WaitForSecondsRealtime)
            .GetConstructor([typeof(float)])));
        il.EmitStfld(currentField);
        il.EmitLdarg(0);
        il.EmitLdcI4(afterYieldDelayState);
        il.EmitStfld(stateField);
        il.EmitLdcI4(1);
        il.EmitStloc(0);
        il.EmitLeave(returnLabel);

        switchInstruction.Operand = switchTargets.ToArray();

        // Cecil can't emit ILLabel operands correctly, it expects Instruction
        foreach (Instruction inst in il.Instrs)
        {
            if (inst.Operand is ILLabel label)
            {
                inst.Operand = label.Target!;
            }

            if (inst.Operand is ILLabel[] labels)
            {
                inst.Operand = labels.Select(x => x.Target).ToArray();
            }
        }
    }
}