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
using System;
using System.Linq;

namespace BindingProxy.Fody
{
    public partial class ModuleWeaver
    {
        private const string WOVEN_PROPERTY_NODE_PROXY_NAME = "Loxodon.Framework.Binding.Proxy.Sources.Weaving.WovenPropertyNodeProxy`2";
        private const string PROPERTY_NODE_PROXY_NAME_SUFFIX = "PropertyNodeProxy";

        protected TypeDefinition CreatePropertyProxy(TypeDefinition sourceTypeDef, PropertyDefinition property)
        {
            var sourceTypeRef = ModuleDefinition.ImportReference(sourceTypeDef.MakeGeneric());
            var propertyTypeRef = ModuleDefinition.ImportReference(property.PropertyType);
            var genericBaseTypeDef = FindTypeDefinition(WOVEN_PROPERTY_NODE_PROXY_NAME);
            var genericInstanceBaseTypeRef = ModuleDefinition.ImportReference(genericBaseTypeDef).MakeGenericInstanceType(sourceTypeRef, propertyTypeRef);
            var sourceFieldRef = ModuleDefinition.ImportReference(genericBaseTypeDef.Fields.FirstOrDefault(x => x.Name == "source")).MakeHostInstanceGeneric(sourceTypeRef, propertyTypeRef);
            var baseCtorRef = ModuleDefinition.ImportReference(genericBaseTypeDef.GetConstructors().FirstOrDefault()).MakeHostInstanceGeneric(sourceTypeRef, propertyTypeRef);
            const TypeAttributes typeAttributes = TypeAttributes.Class | TypeAttributes.NestedPrivate | TypeAttributes.BeforeFieldInit;
            var typeDef = new TypeDefinition(null, property.Name + PROPERTY_NODE_PROXY_NAME_SUFFIX, typeAttributes, genericInstanceBaseTypeRef);
            typeDef.CloneGenericParameters(sourceTypeDef);

            //add constructor method.
            AddCtorMethod(typeDef, baseCtorRef, sourceTypeRef, property);
            //add GetValue method.
            AddGetMethod(typeDef, sourceFieldRef, property);
            //add SetValue method.
            AddSetMethod(typeDef, sourceFieldRef, sourceTypeRef, property);

            AddGeneratedCodeAttribute(typeDef);
            AddDebuggerNonUserCodeAttribute(typeDef);
            AddPreserveAttribute(typeDef);
            return typeDef;
        }

        private void AddCtorMethod(TypeDefinition typeDef, MethodReference baseCtorRef, TypeReference sourceTypeRef, PropertyDefinition property)
        {
            const MethodAttributes attributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName;
            MethodDefinition methodDef = new MethodDefinition(".ctor", attributes, TypeSystem.VoidReference);
            var parameter = new ParameterDefinition("source", ParameterAttributes.None, sourceTypeRef);
            methodDef.Parameters.Add(parameter);
            var body = methodDef.Body;

            var instructions = body.Instructions;
            instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            instructions.Add(Instruction.Create(OpCodes.Ldarg_1));
            //instructions.Add(Instruction.Create(OpCodes.Ldstr, property.Name));
            instructions.Add(Instruction.Create(OpCodes.Call, baseCtorRef));
            instructions.Add(Instruction.Create(OpCodes.Nop));
            instructions.Add(Instruction.Create(OpCodes.Nop));
            instructions.Add(Instruction.Create(OpCodes.Ret));
            body.OptimizeMacros();
            AddPreserveAttribute(methodDef);
            typeDef.Methods.Add(methodDef);
        }

        private void AddGetMethod(TypeDefinition typeDef, FieldReference sourceFieldRef, PropertyDefinition property)
        {
            TypeReference valueTypeRef = ModuleDefinition.ImportReference(property.PropertyType);
            const MethodAttributes attributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual;
            MethodDefinition methodDef = new MethodDefinition("GetValue", attributes, valueTypeRef);
            var body = methodDef.Body;
            body.Variables.Add(new VariableDefinition(valueTypeRef));
            body.InitLocals = true;

            var getMethodRef = property.GetMethod.MakeGeneric();// ModuleDefinition.ImportReference(property.GetMethod).MakeGeneric();

            var instructions = body.Instructions;
            var ldloc = Instruction.Create(OpCodes.Ldloc_0);
            instructions.Add(Instruction.Create(OpCodes.Nop));
            instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            instructions.Add(Instruction.Create(OpCodes.Ldfld, sourceFieldRef));
            instructions.Add(Instruction.Create(OpCodes.Callvirt, getMethodRef));
            instructions.Add(Instruction.Create(OpCodes.Stloc_0));
            instructions.Add(Instruction.Create(OpCodes.Br_S, ldloc));
            instructions.Add(ldloc);
            instructions.Add(Instruction.Create(OpCodes.Ret));
            body.OptimizeMacros();
            AddPreserveAttribute(methodDef);
            typeDef.Methods.Add(methodDef);
        }

        private void AddSetMethod(TypeDefinition typeDef, FieldReference sourceFieldRef, TypeReference sourceTypeRef, PropertyDefinition property)
        {
            TypeReference valueTypeRef = ModuleDefinition.ImportReference(property.PropertyType);
            const MethodAttributes attributes = MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual;
            MethodDefinition methodDef = new MethodDefinition("SetValue", attributes, TypeSystem.VoidReference);
            var parameter = new ParameterDefinition("value", ParameterAttributes.None, valueTypeRef);
            methodDef.Parameters.Add(parameter);
            var body = methodDef.Body;
            var instructions = body.Instructions;


            var setMethod = property.SetMethod;
            if (setMethod != null && setMethod.IsPublic)
            {
                var setMethodRef = setMethod.MakeGeneric();// ModuleDefinition.ImportReference(setMethod).MakeGeneric();

                instructions.Add(Instruction.Create(OpCodes.Nop));
                instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                instructions.Add(Instruction.Create(OpCodes.Ldfld, sourceFieldRef));
                instructions.Add(Instruction.Create(OpCodes.Ldarg_1));
                instructions.Add(Instruction.Create(OpCodes.Callvirt, setMethodRef));
                instructions.Add(Instruction.Create(OpCodes.Nop));
                instructions.Add(Instruction.Create(OpCodes.Ret));
            }
            else
            {
                var message = string.Format("{0}.{1} is read-only or inaccessible.", sourceTypeRef.DisplayName(), property.Name);
                var exception = ModuleDefinition.ImportReference(typeof(MemberAccessException).GetConstructor(new Type[] { typeof(string) }));
                instructions.Add(Instruction.Create(OpCodes.Nop));
                instructions.Add(Instruction.Create(OpCodes.Ldstr, message));
                instructions.Add(Instruction.Create(OpCodes.Newobj, exception));
                instructions.Add(Instruction.Create(OpCodes.Throw));
            }
            body.OptimizeMacros();
            AddPreserveAttribute(methodDef);
            typeDef.Methods.Add(methodDef);
        }
    }
}
