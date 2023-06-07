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
using Mono.Cecil.Rocks;
using System.Linq;

namespace BindingProxy.Fody
{
    static class CecilExtensions
    {
        public static FieldReference MakeHostInstanceGeneric(
        this FieldReference self,
        params TypeReference[] args)
        {
            var reference = new FieldReference(
                self.Name,
                self.FieldType,
                self.DeclaringType.MakeGenericInstanceType(args));
            return reference;
        }



        public static MethodReference MakeHostInstanceGeneric(
        this MethodReference self,
        params TypeReference[] args)
        {
            var reference = new MethodReference(
                self.Name,
                self.ReturnType,
                self.DeclaringType.MakeGenericInstanceType(args))
            {
                HasThis = self.HasThis,
                ExplicitThis = self.ExplicitThis,
                CallingConvention = self.CallingConvention
            };

            foreach (var parameter in self.Parameters)
            {
                reference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));
            }

            foreach (var genericParam in self.GenericParameters)
            {
                reference.GenericParameters.Add(new GenericParameter(genericParam.Name, reference));
            }

            return reference;
        }

        public static bool IsMatch(this MethodReference methodReference, params string[] paramTypes)
        {
            if (methodReference.Parameters.Count != paramTypes.Length)
            {
                return false;
            }
            for (var index = 0; index < methodReference.Parameters.Count; index++)
            {
                var parameterDefinition = methodReference.Parameters[index];
                var paramType = paramTypes[index];
                if (parameterDefinition.ParameterType.Name != paramType)
                {
                    return false;
                }
            }
            return true;
        }

        public static FieldReference MakeGeneric(this FieldReference self)
        {
            if (!self.DeclaringType.HasGenericParameters)
                return self;

            return new FieldReference(self.Name, self.FieldType, self.DeclaringType.MakeGeneric());
        }

        public static MethodReference MakeGeneric(this MethodReference self)
        {
            if (!self.DeclaringType.HasGenericParameters)
                return self;

            var reference = new MethodReference(
                self.Name,
                self.ReturnType,
                self.DeclaringType.MakeGeneric())
            {
                HasThis = self.HasThis,
                ExplicitThis = self.ExplicitThis,
                CallingConvention = self.CallingConvention
            };

            foreach (var parameter in self.Parameters)
            {
                reference.Parameters.Add(new ParameterDefinition(parameter.ParameterType));
            }

            foreach (var genericParam in self.GenericParameters)
            {
                reference.GenericParameters.Add(new GenericParameter(genericParam.Name, reference));
            }

            return reference;
        }

        public static TypeReference MakeGeneric(this TypeReference self)
        {
            if (!self.HasGenericParameters)
                return self;

            var genericInstanceType = new GenericInstanceType(self);
            foreach (var parameter in self.GenericParameters)
            {
                genericInstanceType.GenericArguments.Add(parameter);
            }
            return genericInstanceType;
        }

        public static string DisplayName(this TypeReference typeReference)
        {
            if (typeReference is GenericInstanceType genericInstanceType && genericInstanceType.HasGenericArguments)
            {
                return typeReference.Name.Split('`').First() + "<" + string.Join(", ", genericInstanceType.GenericArguments.Select(c => c.DisplayName())) + ">";
            }
            return typeReference.Name;
        }

        public static void CloneGenericParameters(this TypeDefinition type, TypeDefinition fromType)
        {
            if (!fromType.HasGenericParameters)
                return;

            foreach (var genericParameter in fromType.GenericParameters)
            {
                var genericParam = new GenericParameter(genericParameter.Name, type);
                genericParam.IsValueType = genericParameter.IsValueType;
                genericParam.HasReferenceTypeConstraint = genericParameter.HasReferenceTypeConstraint;
                genericParam.IsContravariant = genericParameter.IsContravariant;
                genericParam.IsCovariant = genericParameter.IsCovariant;
                genericParam.IsNonVariant = genericParameter.IsNonVariant;
                genericParam.Attributes = genericParameter.Attributes;
                genericParam.HasNotNullableValueTypeConstraint = genericParameter.HasNotNullableValueTypeConstraint;
                genericParam.HasDefaultConstructorConstraint = genericParameter.HasDefaultConstructorConstraint;
                foreach (var constraint in genericParameter.Constraints)
                {
                    genericParam.Constraints.Add(constraint);
                }
                type.GenericParameters.Add(genericParam);
            }
        }
    }
}
