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

using System;
using System.Collections.Generic;
using Mono.Cecil;

namespace BindingProxy.Fody
{
    public partial class ModuleWeaver
    {
        private Dictionary<string, bool> typesImplementingINotify = new Dictionary<string, bool>();

        public bool HierarchyImplementsINotify(TypeReference typeReference)
        {
            var fullName = typeReference.FullName;
            if (typesImplementingINotify.TryGetValue(fullName, out var implementsINotify))
                return implementsINotify;

            TypeDefinition typeDefinition;
            if (typeReference.IsDefinition)
            {
                typeDefinition = (TypeDefinition)typeReference;
            }
            else
            {
                try
                {

                    typeDefinition = typeReference.Resolve(); // Resolve(typeReference);
                }
                catch (Exception ex)
                {
                    WriteWarning($"Ignoring type {fullName} in type hierarchy => {ex.Message}");
                    return false;
                }
            }

            foreach (var interfaceImplementation in typeDefinition.Interfaces)
            {
                if (interfaceImplementation.InterfaceType.Name == "INotifyPropertyChanged")
                {
                    typesImplementingINotify[fullName] = true;
                    return true;
                }
            }

            var baseType = typeDefinition.BaseType;
            if (baseType == null || baseType.FullName.Equals(typeof(object).FullName))
            {
                typesImplementingINotify[fullName] = false;
                return false;
            }

            var baseTypeImplementsINotify = HierarchyImplementsINotify(baseType);
            typesImplementingINotify[fullName] = baseTypeImplementsINotify;
            return baseTypeImplementsINotify;
        }
    }
}
