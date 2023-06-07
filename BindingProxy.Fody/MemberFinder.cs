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

using Mono.Cecil;
using System.Collections.Generic;

namespace BindingProxy.Fody
{
    public partial class ModuleWeaver
    {
        private static bool IsPublic(PropertyDefinition property)
        {
            MethodDefinition getter = property.GetMethod;
            if (getter != null && getter.IsPublic)
                return true;

            MethodDefinition setter = property.SetMethod;
            if (setter != null && setter.IsPublic)
                return true;
            return false;
        }

        public IEnumerable<PropertyDefinition> GetProperties(TypeDefinition type)
        {
            if (type == null || type.IsValueType || type.FullName.Equals(typeof(object).FullName))
                yield break;

            foreach (var property in type.Properties)
            {
                if (property.GetMethod == null || !IsPublic(property))
                    continue;

                yield return property;
            }
        }

        public IEnumerable<FieldDefinition> GetFields(TypeDefinition type)
        {
            if (type == null || type.IsValueType || type.FullName.Equals(typeof(object).FullName))
                yield break;

            foreach (var field in type.Fields)
            {
                if (field.IsStatic || !field.IsPublic)
                    continue;

                yield return field;
            }
        }

        public IEnumerable<MethodDefinition> GetMethods(TypeDefinition type)
        {
            if (type == null || type.IsValueType || type.FullName.Equals(typeof(object).FullName))
                yield break;

            foreach (var method in type.Methods)
            {
                if (method.IsSpecialName || method.IsStatic || !method.IsPublic || method.IsConstructor || method.IsAddOn || method.IsRemoveOn || method.IsFire || method.IsGetter || method.IsSetter)
                    continue;

                if (!method.ReturnType.FullName.Equals("System.Void"))
                    continue;

                if (method.Parameters.Count > 4)
                    continue;

                yield return method;
            }
        }

        //public IEnumerable<EventDefinition> GetEvents(TypeDefinition type)
        //{
        //    if (type == null || type.IsValueType || type.FullName.Equals(typeof(object).FullName))
        //        yield break;

        //    foreach (var e in type.Events)
        //    {
        //        yield return e;
        //    }
        //}
    }
}
