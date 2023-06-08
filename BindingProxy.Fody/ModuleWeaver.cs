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
using Mono.Collections.Generic;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mono.Cecil.Rocks;

namespace BindingProxy.Fody
{
    public partial class ModuleWeaver : BaseModuleWeaver
    {
        const string PROPERTY_PROXY_ATTRIBUTE = "GeneratePropertyProxyAttribute";
        const string FIELD_PROXY_ATTRIBUTE = "GenerateFieldProxyAttribute";
        const string METHOD_INVOKER_ATTRIBUTE = "GenerateInvokerAttribute";
        const string IGNORE_ATTRIBUTE = "IgnoreAttribute";
        const string PRESERVE_ATTRIBUTE = "UnityEngine.Scripting.PreserveAttribute";
        const string BINDING_PROXY_NAMESPACE = "BindingProxy";

        public Action<string, string> Log;
        public override void Execute()
        {
            try
            {
                ParseConfig();
                foreach (var type in GetMatchingTypes())
                {
                    bool needFinder = false;
                    needFinder |= WeaveFields(type);
                    needFinder |= WeaveProperties(type);
                    needFinder |= WeaveMethods(type);
                    if (needFinder && !type.IsAbstract)
                        AddProxyFinder(type);

                    RemoveAttributes(type.Properties);
                    RemoveAttributes(type.Fields);
                    RemoveAttributes(type);
                }
            }
            catch (Exception e)
            {
                this.WriteError(e.StackTrace);
                throw e;
            }
        }

        public override IEnumerable<string> GetAssembliesForScanning()
        {
            yield return "mscorlib";
            yield return "System";
            yield return "System.Runtime";
            yield return "netstandard";
            yield return "UnityEngine";
            yield return "UnityEngine.CoreModule";
            yield return "Loxodon.Framework";
            yield return "Loxodon.Framework.Binding.Weaving";
        }

        protected bool WeaveProperties(TypeDefinition typeDef)
        {
            bool ret = false;
            bool defaultWeave = this.defaultWeaveProperty | HasProxyAttribute(typeDef, PROPERTY_PROXY_ATTRIBUTE);
            var properties = this.GetProperties(typeDef);
            foreach (var property in properties)
            {
                if (IsIgnore(property))
                    continue;

                if (defaultWeave || HasProxyAttribute(property, PROPERTY_PROXY_ATTRIBUTE))
                {
                    var proxyDef = CreatePropertyProxy(typeDef, property);
                    typeDef.NestedTypes.Add(proxyDef);
                    ret |= true;
                }
            }
            return ret;
        }

        protected bool WeaveFields(TypeDefinition typeDef)
        {
            bool ret = false;
            bool defaultWeave = this.defaultWeaveField | HasProxyAttribute(typeDef, FIELD_PROXY_ATTRIBUTE);
            var fields = GetFields(typeDef);
            foreach (var field in fields)
            {
                if (IsIgnore(field))
                    continue;

                if (defaultWeave || HasProxyAttribute(field, FIELD_PROXY_ATTRIBUTE))
                {
                    var proxyDef = CreateFieldProxy(typeDef, field);
                    typeDef.NestedTypes.Add(proxyDef);
                    ret |= true;
                }
            }
            return ret;
        }

        protected bool WeaveMethods(TypeDefinition typeDef)
        {
            bool ret = false;
            Dictionary<string, List<MethodDefinition>> methods = new Dictionary<string, List<MethodDefinition>>();
            foreach (var method in GetMethods(typeDef))
            {
                if (!HasProxyAttribute(method, METHOD_INVOKER_ATTRIBUTE))
                    continue;

                RemoveAttributes(method);

                string name = method.Name;
                List<MethodDefinition> list;
                if (!methods.TryGetValue(name, out list))
                {
                    list = new List<MethodDefinition>();
                    methods.Add(name, list);
                }
                list.Add(method);
            }

            foreach (var kv in methods)
            {
                var name = kv.Key;
                var list = kv.Value;
                if (list.Count <= 0)
                    continue;

                var proxyDef = CreateMethodProxy(typeDef, name, list);
                typeDef.NestedTypes.Add(proxyDef);
                ret |= true;
            }
            return ret;
        }

        public IEnumerable<TypeDefinition> GetMatchingTypes()
        {
            return ModuleDefinition.GetTypes()
                .Where(x => Filter(x));
        }

        private bool Filter(TypeDefinition type)
        {
            if (IsIgnore(type))
                return false;

            if (!HierarchyImplementsINotify(type))
                return false;

            return true;
        }

        private bool IsIgnore(IMemberDefinition member)
        {
            if (HasProxyAttribute(member, IGNORE_ATTRIBUTE))
                return true;
            return false;
        }

        private bool HasProxyAttribute(IMemberDefinition member, string name)
        {
            if (member.CustomAttributes.Any(a => a.AttributeType.Namespace == BINDING_PROXY_NAMESPACE && a.AttributeType.Name == name))
                return true;
            return false;
        }

        protected void AddTypeAttributes(IMemberDefinition member)
        {
            if (member is TypeDefinition || member is PropertyDefinition || member is FieldDefinition || member is MethodDefinition)
            {
                var generatedConstructor = ModuleDefinition.ImportReference(typeof(GeneratedCodeAttribute)
                .GetConstructor(new[]
                {
                    typeof(string),
                    typeof(string)
                }));

                var version = typeof(ModuleWeaver).Assembly.GetName().Version.ToString();
                var generatedAttribute = new CustomAttribute(generatedConstructor);
                generatedAttribute.ConstructorArguments.Add(new CustomAttributeArgument(TypeSystem.StringReference, "BindingProxy.Fody"));
                generatedAttribute.ConstructorArguments.Add(new CustomAttributeArgument(TypeSystem.StringReference, version));
                member.CustomAttributes.Add(generatedAttribute);
            }
            if (member is TypeDefinition || member is PropertyDefinition || member is MethodDefinition)
            {
                var debuggerConstructor = ModuleDefinition.ImportReference(typeof(DebuggerNonUserCodeAttribute).GetConstructor(Type.EmptyTypes));
                var debuggerAttribute = new CustomAttribute(debuggerConstructor);
                member.CustomAttributes.Add(debuggerAttribute);
            }

            if (member is TypeDefinition)
            {
                var preserveConstructor = ModuleDefinition.ImportReference(FindTypeDefinition(PRESERVE_ATTRIBUTE).GetConstructors().FirstOrDefault());
                var preserveAttribute = new CustomAttribute(preserveConstructor);
                member.CustomAttributes.Add(preserveAttribute);
            }
        }

        static void RemoveAttributes(TypeDefinition typeDef)
        {
            var customAttributes = typeDef.CustomAttributes;
            foreach (var attribute in customAttributes.ToArray())
            {
                if (BINDING_PROXY_NAMESPACE.Equals(attribute.AttributeType.Namespace))
                    customAttributes.Remove(attribute);
            }
        }

        static void RemoveAttributes(Collection<PropertyDefinition> properties)
        {
            foreach (var property in properties)
            {
                var customAttributes = property.CustomAttributes;
                foreach (var attribute in customAttributes.ToArray())
                {
                    if (BINDING_PROXY_NAMESPACE.Equals(attribute.AttributeType.Namespace))
                        customAttributes.Remove(attribute);
                }
            }
        }

        static void RemoveAttributes(Collection<FieldDefinition> fields)
        {
            foreach (var field in fields)
            {
                var customAttributes = field.CustomAttributes;
                foreach (var attribute in customAttributes.ToArray())
                {
                    if (BINDING_PROXY_NAMESPACE.Equals(attribute.AttributeType.Namespace))
                        customAttributes.Remove(attribute);
                }
            }
        }

        static void RemoveAttributes(MethodDefinition method)
        {
            var customAttributes = method.CustomAttributes;
            foreach (var attribute in customAttributes.ToArray())
            {
                if (BINDING_PROXY_NAMESPACE.Equals(attribute.AttributeType.Namespace))
                    customAttributes.Remove(attribute);
            }
        }
    }
}
