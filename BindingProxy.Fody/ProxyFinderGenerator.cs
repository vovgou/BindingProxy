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
        private const string WOVEN_NODE_PROXY_FINDER_INTERFACE_NAME = "Loxodon.Framework.Binding.Proxy.Sources.Weaving.IWovenNodeProxyFinder";
        private const string WOVEN_NODE_PROXY_FINDER_IMPL_NAME = "Loxodon.Framework.Binding.Proxy.Sources.Weaving.WovenNodeProxyFinder";
        private const string SOURCE_PROXY_INTERFACE_NAME = "Loxodon.Framework.Binding.Proxy.Sources.ISourceProxy";
        protected void AddProxyFinder(TypeDefinition typeDef)
        {
            if (typeDef.IsAbstract)
                return;

            //add the IWovenNodeProxyFinder interface
            var proxyFinderInterfaceTypeDef = FindTypeDefinition(WOVEN_NODE_PROXY_FINDER_INTERFACE_NAME);
            var proxyFinderInterfaceTypeRef = ModuleDefinition.ImportReference(proxyFinderInterfaceTypeDef);
            typeDef.Interfaces.Add(new InterfaceImplementation(proxyFinderInterfaceTypeRef));

            //add _finder field
            var proxyFinderImplTypeDef = FindTypeDefinition(WOVEN_NODE_PROXY_FINDER_IMPL_NAME);
            var proxyFinderImplTypeRef = ModuleDefinition.ImportReference(proxyFinderImplTypeDef);
            const FieldAttributes fieldAttributes = FieldAttributes.Private;
            var fieldDef = new FieldDefinition("_finder", fieldAttributes, proxyFinderImplTypeRef);
            AddTypeAttributes(fieldDef);
            typeDef.Fields.Add(fieldDef);

            //Implement the IWovenNodeProxyFinder interface
            var sourceProxyTypeDef = FindTypeDefinition(SOURCE_PROXY_INTERFACE_NAME);
            var sourceProxyTypeRef = ModuleDefinition.ImportReference(sourceProxyTypeDef);
            const MethodAttributes methodAttributes = MethodAttributes.Private | MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual;
            var methodDef = new MethodDefinition(proxyFinderInterfaceTypeDef.FullName + ".GetSourceProxy", methodAttributes, sourceProxyTypeRef);
            methodDef.Parameters.Add(new ParameterDefinition("name", ParameterAttributes.None, TypeSystem.StringReference));
            AddTypeAttributes(methodDef);

            var originalMethodDef = proxyFinderInterfaceTypeDef.Methods.FirstOrDefault(x => x.Name.Equals("GetSourceProxy"));
            var originalMethodRef = ModuleDefinition.ImportReference(originalMethodDef);
            methodDef.Overrides.Add(originalMethodRef);

            var proxyFinderCtorMethodRef = ModuleDefinition.ImportReference(proxyFinderImplTypeDef.GetConstructors().FirstOrDefault());
            var proxyFinderGetSourceProxyMethodRef = ModuleDefinition.ImportReference(proxyFinderImplTypeDef.Methods.FirstOrDefault(x => x.Name.Equals("GetSourceProxy")));
            var genericField = fieldDef.MakeGeneric();

            var body = methodDef.Body;
            body.Variables.Add(new VariableDefinition(TypeSystem.BooleanReference));
            body.Variables.Add(new VariableDefinition(sourceProxyTypeRef));
            body.InitLocals = true;

            var instructions = body.Instructions;
            var ldarg0 = Instruction.Create(OpCodes.Ldarg_0);
            var end = Instruction.Create(OpCodes.Ldloc_1);

            instructions.Add(Instruction.Create(OpCodes.Nop));
            instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            instructions.Add(Instruction.Create(OpCodes.Ldfld, genericField));
            instructions.Add(Instruction.Create(OpCodes.Ldnull));
            instructions.Add(Instruction.Create(OpCodes.Ceq));
            instructions.Add(Instruction.Create(OpCodes.Stloc_0));
            instructions.Add(Instruction.Create(OpCodes.Ldloc_0));
            instructions.Add(Instruction.Create(OpCodes.Brfalse_S, ldarg0));

            // finder = new WovenNodeProxyFinder(this);
            instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
            instructions.Add(Instruction.Create(OpCodes.Newobj, proxyFinderCtorMethodRef));
            instructions.Add(Instruction.Create(OpCodes.Stfld, genericField));

            // return finder.GetSourceProxy(name);
            instructions.Add(ldarg0);
            instructions.Add(Instruction.Create(OpCodes.Ldfld, genericField));
            instructions.Add(Instruction.Create(OpCodes.Ldarg_1));
            instructions.Add(Instruction.Create(OpCodes.Callvirt, proxyFinderGetSourceProxyMethodRef));
            instructions.Add(Instruction.Create(OpCodes.Stloc_1));
            instructions.Add(Instruction.Create(OpCodes.Br_S, end));

            instructions.Add(end);
            instructions.Add(Instruction.Create(OpCodes.Ret));

            typeDef.Methods.Add(methodDef);
        }
    }
}
