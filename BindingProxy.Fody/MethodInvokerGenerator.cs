/*
 * MIT License
 *
 * Copyright (c) 2018 Clark Yang
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy of 
 * this software and associated documentation files (the "Software"), to deal in 
 * the Software without restriction, including without limitation the rights to 
 * use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies 
 * of the Software, and to permit persons to whom the Software is furnished to do so, 
 * subject to the following conditions:
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

using Fody;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;
using System.Collections.Generic;
using System.Linq;

namespace BindingProxy.Fody
{
    public partial class ModuleWeaver
    {
        private const string WOVEN_METHOD_INVOKER_NAME = "Loxodon.Framework.Binding.Proxy.Sources.Object.WovenMethodInvoker`1";
        private const string WOVEN_INVOKER_NAME_PREFIX = "Loxodon.Framework.Binding.IInvoker`";
        private const string METHOD_INVOKER_NAME_SUFFIX = "MethodInvoker";

        protected TypeDefinition CreateMethodInvoker(TypeDefinition sourceTypeDef, string methodName, List<MethodDefinition> methods)
        {
            var sourceTypeRef = ModuleDefinition.ImportReference(sourceTypeDef);
            var genericBaseTypeDef = FindTypeDefinition(WOVEN_METHOD_INVOKER_NAME);
            var genericInstanceBaseType = genericBaseTypeDef.MakeGenericInstanceType(sourceTypeDef);
            var genericInstanceBaseTypeRef = ModuleDefinition.ImportReference(genericInstanceBaseType);
            var sourceFieldRef = ModuleDefinition.ImportReference(genericBaseTypeDef.Fields.FirstOrDefault(x => x.Name == "source")).MakeHostInstanceGeneric(sourceTypeDef);
            var baseCtorRef = ModuleDefinition.ImportReference(genericBaseTypeDef.GetConstructors().FirstOrDefault()).MakeHostInstanceGeneric(sourceTypeRef);

            const TypeAttributes typeAttributes = TypeAttributes.Class | TypeAttributes.NestedPublic | TypeAttributes.BeforeFieldInit;
            var typeDef = new TypeDefinition(null, methodName + METHOD_INVOKER_NAME_SUFFIX, typeAttributes, genericInstanceBaseTypeRef);

            List<MethodDefinition> invokers = new List<MethodDefinition>();
            //add constructor method.
            AddInvokerCtorMethod(typeDef, baseCtorRef, sourceTypeRef);
            //add interfaces
            foreach (MethodDefinition method in methods)
            {
                AddInvokerInterface(typeDef, method);
                var invoker = AddInvokeMethod(typeDef, sourceFieldRef, method);
                invokers.Add(invoker);
            }

            AddInvokeMethod(typeDef, invokers);
            AddTypeAttributes(typeDef);
            return typeDef;
        }

        private TypeReference[] MakeGenericParameters(Collection<ParameterDefinition> parameters)
        {
            TypeReference[] parameterRefs = new TypeReference[parameters.Count];
            for (int i = 0; i < parameters.Count; i++)
            {
                parameterRefs[i] = ModuleDefinition.ImportReference(parameters[i].ParameterType);
            }
            return parameterRefs;
        }

        private void AddInvokerCtorMethod(TypeDefinition typeDef, MethodReference baseCtorRef, TypeReference sourceTypeRef)
        {
            const MethodAttributes attributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
            MethodDefinition methodDef = new MethodDefinition(".ctor", attributes, TypeSystem.VoidReference);
            var parameter = new ParameterDefinition("source", ParameterAttributes.None, sourceTypeRef);
            methodDef.Parameters.Add(parameter);
            var body = methodDef.Body;

            var instructions = body.Instructions;
            instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            instructions.Add(Instruction.Create(OpCodes.Ldarg_1));
            instructions.Add(Instruction.Create(OpCodes.Call, baseCtorRef));
            instructions.Add(Instruction.Create(OpCodes.Nop));
            instructions.Add(Instruction.Create(OpCodes.Nop));
            instructions.Add(Instruction.Create(OpCodes.Ret));
            body.OptimizeMacros();
            typeDef.Methods.Add(methodDef);
        }

        private void AddInvokerInterface(TypeDefinition typeDef, MethodDefinition method)
        {
            var parameters = method.Parameters;
            if (parameters == null || parameters.Count <= 0)
                return;

            TypeReference[] parameterRefs = MakeGenericParameters(parameters);
            var genericInvokerTypeDef = FindTypeDefinition(WOVEN_INVOKER_NAME_PREFIX + parameters.Count.ToString());
            var genericInstanceInvokerType = genericInvokerTypeDef.MakeGenericInstanceType(parameterRefs);
            var genericInstanceInvokerTypeRef = ModuleDefinition.ImportReference(genericInstanceInvokerType);
            typeDef.Interfaces.Add(new InterfaceImplementation(genericInstanceInvokerTypeRef));
        }

        private void AddInvokeMethod(TypeDefinition typeDef, List<MethodDefinition> invokers)
        {
            const MethodAttributes attributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual;
            MethodDefinition methodDef = new MethodDefinition("Invoke", attributes, TypeSystem.ObjectReference);
            var param = new ParameterDefinition("args", ParameterAttributes.None, ModuleDefinition.ImportReference(typeof(object[])));
            var ctor = ModuleDefinition.ImportReference(FindTypeDefinition("System.ParamArrayAttribute").GetConstructors().FirstOrDefault());
            param.CustomAttributes.Add(new CustomAttribute(ctor));
            methodDef.Parameters.Add(param);

            var body = methodDef.Body;
            body.Variables.Add(new VariableDefinition(TypeSystem.Int32Reference));
            body.Variables.Add(new VariableDefinition(TypeSystem.Int32Reference));
            body.Variables.Add(new VariableDefinition(TypeSystem.ObjectReference));
            body.InitLocals = true;

            var instructions = body.Instructions;
            var end = Instruction.Create(OpCodes.Ldloc_2);
            var defaultCase = SwitchCaseDefault(end);
            var ldci40 = Instruction.Create(OpCodes.Ldc_I4_0);
            var stloc0 = Instruction.Create(OpCodes.Stloc_0);

            instructions.Add(Instruction.Create(OpCodes.Nop));
            instructions.Add(Instruction.Create(OpCodes.Ldarg_1));
            instructions.Add(Instruction.Create(OpCodes.Brfalse_S, ldci40));

            instructions.Add(Instruction.Create(OpCodes.Ldarg_1));
            instructions.Add(Instruction.Create(OpCodes.Ldlen));
            instructions.Add(Instruction.Create(OpCodes.Conv_I4));
            instructions.Add(Instruction.Create(OpCodes.Br_S, stloc0));

            instructions.Add(ldci40);
            instructions.Add(stloc0);
            instructions.Add(Instruction.Create(OpCodes.Ldloc_0));
            instructions.Add(Instruction.Create(OpCodes.Stloc_1));
            instructions.Add(Instruction.Create(OpCodes.Ldloc_1));


            List<Instruction> switchInstructions = new List<Instruction>();
            List<Instruction> switchCaseBlockInstructions = new List<Instruction>();
            for (int i = 0; i < 5; i++)
            {
                var method = invokers.Find(x => x.Parameters.Count == i);
                if (method == null)
                    switchInstructions.Add(defaultCase[0]);
                else
                {
                    var a = SwitchCase(method, end);
                    switchInstructions.Add(a[0]);
                    switchCaseBlockInstructions.AddRange(a);
                }
            }

            //switch
            instructions.Add(Instruction.Create(OpCodes.Switch, switchInstructions.ToArray()));
            instructions.Add(Instruction.Create(OpCodes.Br, defaultCase[0]));

            //switch case block
            foreach (var instruction in switchCaseBlockInstructions)
            {
                instructions.Add(instruction);
            }

            // return null;
            foreach (var instruction in defaultCase)
            {
                instructions.Add(instruction);
            }

            instructions.Add(end);
            instructions.Add(Instruction.Create(OpCodes.Ret));
            body.OptimizeMacros();
            typeDef.Methods.Add(methodDef);
        }

        private List<Instruction> SwitchCaseDefault(Instruction end)
        {
            List<Instruction> instructions = new List<Instruction>();
            instructions.Add(Instruction.Create(OpCodes.Ldnull));
            instructions.Add(Instruction.Create(OpCodes.Stloc_2));
            instructions.Add(Instruction.Create(OpCodes.Br_S, end));
            return instructions;
        }

        private List<Instruction> SwitchCase(MethodDefinition method, Instruction end)
        {
            List<Instruction> instructions = new List<Instruction>();
            var parameters = method.Parameters;

            instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            for (int i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                var paramType = parameter.ParameterType;
                instructions.Add(Instruction.Create(OpCodes.Ldarg_1));
                instructions.Add(Instruction.Create(OpCodes.Ldc_I4, i));
                instructions.Add(Instruction.Create(OpCodes.Ldelem_Ref));

                if (paramType.IsValueType)
                    instructions.Add(Instruction.Create(OpCodes.Unbox_Any, paramType));
                else
                    instructions.Add(Instruction.Create(OpCodes.Castclass, paramType));
            }
            instructions.Add(Instruction.Create(OpCodes.Call, method));
            instructions.Add(Instruction.Create(OpCodes.Stloc_2));
            instructions.Add(Instruction.Create(OpCodes.Br_S, end));
            return instructions;
        }

        private MethodDefinition AddInvokeMethod(TypeDefinition typeDef, FieldReference sourceFieldRef, MethodDefinition method)
        {
            int paramCount = method.Parameters.Count;
            MethodAttributes attributes = MethodAttributes.Public | MethodAttributes.HideBySig;
            if (paramCount > 0)
                attributes |= MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot;

            MethodDefinition methodDef = new MethodDefinition("Invoke", attributes, TypeSystem.ObjectReference);
            foreach (var param in method.Parameters)
            {
                methodDef.Parameters.Add(new ParameterDefinition(param.Name, ParameterAttributes.None, param.ParameterType));
            }

            var body = methodDef.Body;
            body.Variables.Add(new VariableDefinition(TypeSystem.ObjectReference));
            body.InitLocals = true;

            var instructions = body.Instructions;
            var ldloc = Instruction.Create(OpCodes.Ldloc_0);
            instructions.Add(Instruction.Create(OpCodes.Nop));
            instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            instructions.Add(Instruction.Create(OpCodes.Ldfld, sourceFieldRef));
            foreach (var parameter in methodDef.Parameters)
            {
                instructions.Add(Instruction.Create(OpCodes.Ldarg_S, parameter));
            }
            instructions.Add(Instruction.Create(OpCodes.Callvirt, ModuleDefinition.ImportReference(method)));
            instructions.Add(Instruction.Create(OpCodes.Nop));
            instructions.Add(Instruction.Create(OpCodes.Ldnull));
            instructions.Add(Instruction.Create(OpCodes.Stloc_0));
            instructions.Add(Instruction.Create(OpCodes.Br_S, ldloc));
            instructions.Add(ldloc);
            instructions.Add(Instruction.Create(OpCodes.Ret));
            body.OptimizeMacros();
            typeDef.Methods.Add(methodDef);
            return methodDef;
        }
    }
}
