using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Unity.ClusterDisplay.RPC.ILPostProcessing
{
    internal partial class RPCILPostProcessor
    {
        sealed class QueuedRPCILGenerator
        {
            RPCILPostProcessor rpcILPostProcessor;
            ILProcessor ilProcessor;
            Instruction lastSwitchCaseInstruction;
            TypeReference generatedRPCILTypeRef;

            public QueuedRPCILGenerator (RPCILPostProcessor rpcILPostProcessor, TypeReference generatedRPCILTypeRef)
            {
                this.rpcILPostProcessor = rpcILPostProcessor;
                this.generatedRPCILTypeRef = generatedRPCILTypeRef;
            }

            public bool TrySetup ()
            {
                if (!TryGetCachedExecuteQueuedRPCMethodILProcessor(
                    out var queuedILProcessor))
                    return false;
                return true;
            }

            Dictionary<string, Instruction> firstExecutionInstruction = new Dictionary<string, Instruction>();
            public bool TryInjectILToExecuteQueuedRPC(
                MethodReference targetMethod,
                RPCExecutionStage rpcExecutionStage,
                string rpcHash)
            {
                if (lastSwitchCaseInstruction == null)
                    lastSwitchCaseInstruction = ilProcessor.Body.Instructions[0];
                var lastInstruction = ilProcessor.Body.Instructions[ilProcessor.Body.Instructions.Count - 2];
                
                Instruction firstInstructionOfCaseImpl;
                var targetMethodDef = targetMethod.Resolve();
                if (targetMethodDef.IsStatic)
                {
                     if (!rpcILPostProcessor.TryInjectStaticRPCExecution(
                        generatedRPCILTypeRef.Module,
                        ilProcessor,
                        beforeInstruction: lastInstruction,
                        targetMethodRef: targetMethod,
                        firstInstructionOfInjection: out firstInstructionOfCaseImpl))
                        return false;
                }

                else if (!rpcILPostProcessor.TryInjectInstanceRPCExecution(
                    generatedRPCILTypeRef.Module,
                    ilProcessor,
                    beforeInstruction: lastInstruction,
                    executionTarget: targetMethod,
                    firstInstructionOfInjection: out firstInstructionOfCaseImpl))
                    return false;
                
                if (!rpcILPostProcessor.TryInjectSwitchCaseForRPC(
                    ilProcessor,
                    valueToPushForBeq: rpcHash,
                    jmpToInstruction: firstInstructionOfCaseImpl,
                    afterInstruction: ref lastSwitchCaseInstruction))
                    return false;

                return true;
            }

            bool TryGetCachedExecuteQueuedRPCMethodILProcessor (
                out ILProcessor ilProcessor)
            {
                if (this.ilProcessor != null)
                {
                    ilProcessor = this.ilProcessor;
                    return true;
                }

                if (!rpcILPostProcessor.cecilUtils.TryImport(generatedRPCILTypeRef.Module, typeof(RPCInterfaceRegistry.OnTryCallQueuedInstanceImplementationAttribute), out var executeQueuedRPCAttributeTypeRef))
                {
                    ilProcessor = null;
                    return false;
                }

                if (!rpcILPostProcessor.TryFindMethodReferenceWithAttributeInModule(
                    generatedRPCILTypeRef.Module,
                    generatedRPCILTypeRef.Resolve(),
                    executeQueuedRPCAttributeTypeRef,
                    out var methodRef))
                {
                    ilProcessor = null;
                    return false;
                }

                var methodDef = methodRef.Resolve();
                ilProcessor = methodDef.Body.GetILProcessor();

                this.ilProcessor = ilProcessor;

                return true;
            }

            public bool InjectDefaultSwitchCase()
            {
                if (lastSwitchCaseInstruction == null)
                    return true;

                var isntructionToJmpTo = ilProcessor.Body.Instructions[ilProcessor.Body.Instructions.Count - 2];
                var newInstruction = Instruction.Create(OpCodes.Br, isntructionToJmpTo);
                ilProcessor.InsertAfter(lastSwitchCaseInstruction, newInstruction);
                return true;
            }
        }
    }
}
