﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;


namespace Norm.Extensions
{
    public static partial class NormExtensions
    {
        private static readonly Dictionary<int, IDictionary<byte, Delegate>> TypeCache =
            new Dictionary<int, IDictionary<byte, Delegate>>();
        private static readonly Dictionary<int, HashSet<byte>> NullableCache =
            new Dictionary<int, HashSet<byte>>();
        private static readonly Dictionary<int, (ConstructorInfo parametlessCtor, ConstructorInfo paramsCtor, ParameterInfo[] parameters)> CtorCache =
            new Dictionary<int, (ConstructorInfo parametlessCtor, ConstructorInfo paramsCtor, ParameterInfo[] parameters)>();

        private static T SelectInternal<T>(
            this IEnumerable<(string name, object value)> tuples,
            T instance,
            int instanceHashCode,
            IReflect instanceType, 
            IDictionary<byte, Delegate> typeDict,
            HashSet<byte> nullableHash,
            bool isNewEntry)
        {
            foreach (var ((name, value), index) in tuples.Select((item, index) => ((item.name.Replace("_", ""), item.value), (byte)index)))
            {
                void SetValue<TProp>(TProp propertyValue) where TProp : struct
                {
                    if (typeDict.TryGetValue(index, out var cachedSetter))
                    {
                        if (!nullableHash.Contains(index))
                        {
                            ((Action<T, TProp>)cachedSetter).Invoke(instance, propertyValue);
                            return;
                        }
                        ((Action<T, TProp?>)cachedSetter).Invoke(instance, propertyValue);
                        return;
                    }

                    var propertyInfo = instanceType.GetProperty(name, BindingFlags.IgnoreCase | BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (propertyInfo == null)
                    {
                        return;
                    }

                    var reflectionSetter = propertyInfo.GetSetMethod(true);
                    var nullable = Nullable.GetUnderlyingType(propertyInfo.PropertyType) != null;
                    if (!nullable)
                    {
                        var setter = Delegate.CreateDelegate(typeof(Action<T, TProp>), reflectionSetter);
                        typeDict.Add(index, setter);
                        ((Action<T, TProp>)setter).Invoke(instance, propertyValue);
                    }
                    else
                    {
                        var setter = Delegate.CreateDelegate(typeof(Action<T, TProp?>), reflectionSetter);
                        typeDict.Add(index, setter);
                        nullableHash.Add(index);
                        ((Action<T, TProp?>)setter).Invoke(instance, propertyValue);
                    }
                }

                void SetSpanValue<TProp>(ReadOnlySpan<TProp> propertyValue) //where TProp : struct
                {
                    if (typeDict.TryGetValue(index, out var cachedSetter))
                    {
                        ((Action<T, TProp[]>)cachedSetter).Invoke(instance, propertyValue.ToArray());
                        return;
                    }
                    var propertyInfo = instanceType.GetProperty(name, BindingFlags.IgnoreCase | BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (propertyInfo == null)
                    {
                        return;
                    }
                    var reflectionSetter = propertyInfo.GetSetMethod(true);
                    var setter = Delegate.CreateDelegate(typeof(Action<T, TProp[]>), reflectionSetter);
                    typeDict.Add(index, setter);
                    ((Action<T, TProp[]>)setter).Invoke(instance, propertyValue.ToArray() as TProp[]);
                }

                void SetStringValue(ReadOnlySpan<char> propertyValue)
                {
                    if (typeDict.TryGetValue(index, out var cachedSetter))
                    {
                        ((Action<T, string>)cachedSetter).Invoke(instance, propertyValue.ToString());
                        return;
                    }
                    var propertyInfo = instanceType.GetProperty(name, BindingFlags.IgnoreCase | BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (propertyInfo == null)
                    {
                        return;
                    }
                    var reflectionSetter = propertyInfo.GetSetMethod(true);
                    var setter = Delegate.CreateDelegate(typeof(Action<T, string>), reflectionSetter);
                    typeDict.Add(index, setter);
                    ((Action<T, string>)setter).Invoke(instance, propertyValue.ToString());
                }

                TypeCode code;
                bool isArray;
                var type = value.GetType();
                if (type.IsArray)
                {
                    isArray = true;
                    code = Type.GetTypeCode(type.GetElementType());
                }
                else
                {
                    isArray = false;
                    code = Type.GetTypeCode(type);
                }

                switch (code)
                {
                    case TypeCode.Boolean:
                        if (!isArray) SetValue((bool)value); else SetSpanValue<bool>(new ReadOnlySpan<bool>((bool[])value));
                        continue;
                    case TypeCode.Byte:
                        if (!isArray) SetValue((byte)value); else SetSpanValue<byte>(new ReadOnlySpan<byte>((byte[])value));
                        continue;
                    case TypeCode.Char:
                        if (!isArray) SetValue((char)value); else SetSpanValue<char>(new ReadOnlySpan<char>((char[])value));
                        continue;
                    case TypeCode.DateTime:
                        if (!isArray) SetValue((DateTime)value); else SetSpanValue<DateTime>(new ReadOnlySpan<DateTime>((DateTime[])value));
                        continue;
                    case TypeCode.Decimal:
                        if (!isArray) SetValue((decimal)value); else SetSpanValue<decimal>(new ReadOnlySpan<decimal>((decimal[])value));
                        continue;
                    case TypeCode.Double:
                        if (!isArray) SetValue((double)value); else SetSpanValue<double>(new ReadOnlySpan<double>((double[])value));
                        continue;
                    case TypeCode.Int16:
                        if (!isArray) SetValue((short)value); else SetSpanValue<short>(new ReadOnlySpan<short>((short[])value));
                        continue;
                    case TypeCode.Int32:
                        if (!isArray) SetValue((int) value); else SetSpanValue<int>(new ReadOnlySpan<int>((int[])value));
                        continue;
                    case TypeCode.Int64:
                        if (!isArray) SetValue((long)value); else SetSpanValue<long>(new ReadOnlySpan<long>((long[])value));
                        continue;
                    case TypeCode.SByte:
                        if (!isArray) SetValue((sbyte)value); else SetSpanValue<sbyte>(new ReadOnlySpan<sbyte>((sbyte[])value));
                        continue;
                    case TypeCode.Single:
                        if (!isArray) SetValue((float)value); else SetSpanValue<float>(new ReadOnlySpan<float>((float[])value));
                        continue;
                    case TypeCode.String:
                        if (!isArray) SetStringValue((string)value); else SetSpanValue<string>(new ReadOnlySpan<string>((string[])value));
                        continue;
                    case TypeCode.UInt16:
                        if (!isArray) SetValue((ushort)value); else SetSpanValue<ushort>(new ReadOnlySpan<ushort>((ushort[])value));
                        continue;
                    case TypeCode.UInt32:
                        if (!isArray) SetValue((uint)value); else SetSpanValue<uint>(new ReadOnlySpan<uint>((uint[])value));
                        continue;
                    case TypeCode.UInt64:
                        if (!isArray) SetValue((ulong)value); else SetSpanValue<ulong>(new ReadOnlySpan<ulong>((ulong[])value));
                        continue;
                }
            }

            if (!isNewEntry) return instance;
            TypeCache.TryAdd(instanceHashCode, typeDict);
            NullableCache.TryAdd(instanceHashCode, nullableHash);
            return instance;
        }

        private static IEnumerable<T> SelectInternal<T>(this IEnumerable<IList<(string name, object value)>> tuples, 
            Type instanceType, ConstructorInfo ctor, int instanceHashCode)
        {
            return tuples.Select(t =>
            {
                var instance = (T)ctor.Invoke(Array.Empty<object>());

                if (TypeCache.TryGetValue(instanceHashCode, out var dict))
                {
                    return t.SelectInternal(instance, instanceHashCode, instanceType, dict, NullableCache[instanceHashCode], false);
                }

                dict = new Dictionary<byte, Delegate>();
                var nullableHash = new HashSet<byte>();
                return t.SelectInternal(instance, instanceHashCode, instanceType, dict, nullableHash, true);
            });
        }

        public static async IAsyncEnumerable<T> SelectInternal<T>(this IAsyncEnumerable<IList<(string name, object value)>> tuples, 
            Type instanceType, ConstructorInfo ctor, int instanceHashCode)
        {
            await foreach (var t in tuples)
            {
                var instance = (T)ctor.Invoke(Array.Empty<object>());

                if (TypeCache.TryGetValue(instanceHashCode, out var dict))
                {
                    yield return t.SelectInternal(instance, instanceHashCode, instanceType, dict, NullableCache[instanceHashCode], false);
                    continue;
                }

                dict = new Dictionary<byte, Delegate>();
                var nullableHash = new HashSet<byte>();
                yield return t.SelectInternal(instance, instanceHashCode, instanceType, dict, nullableHash, true);
            }
        }

        private static (ConstructorInfo parametlessCtor, ConstructorInfo paramsCtor, ParameterInfo[] parameters) GetCtors(Type type, int instanceHashCode)
        {
            if (CtorCache.TryGetValue(instanceHashCode, out var result))
            {
                return result;
            }
            ConstructorInfo parametlessCtor = null;
            ConstructorInfo paramsCtor = null;
            ParameterInfo[] parameters = null;
            foreach (var ctor in type.GetConstructors())
            {
                var p = ctor.GetParameters();
                var len = p.Length;
                if (len == 0)
                {
                    parametlessCtor = ctor;
                }
                if (len != 0 && (paramsCtor == null || parameters.Length < len))
                {
                    paramsCtor = ctor;
                    parameters = p;
                }
            }
            CtorCache.TryAdd(instanceHashCode, (parametlessCtor, paramsCtor, parameters));
            return (parametlessCtor, paramsCtor, parameters);
        }
        
        public static IEnumerable<T> Select<T>(this IEnumerable<IList<(string name, object value)>> tuples)
        {
            var instanceType = typeof(T);
            var instanceHashCode = instanceType.GetHashCode();
            var (parametlessCtor, paramsCtor, parameters) = GetCtors(instanceType, instanceHashCode);
            
            if (paramsCtor == null && parametlessCtor != null)
            {
                return tuples.SelectInternal<T>(instanceType, parametlessCtor, instanceHashCode);
            }
            else if (paramsCtor != null)
            {
                return tuples.Select(record => (T)paramsCtor.Invoke(record.Select(r => r.value == DBNull.Value ? null : r.value).ToArray()));
            }
            else
            {
                throw new ArgumentException();
            }
        }

        public static async IAsyncEnumerable<T> Select<T>(this IAsyncEnumerable<IList<(string name, object value)>> tuples)
        {
            var instanceType = typeof(T);
            var instanceHashCode = instanceType.GetHashCode();
            var (parametlessCtor, paramsCtor, parameters) = GetCtors(instanceType, instanceHashCode);

            if (paramsCtor == null && parametlessCtor != null)
            {
                await foreach (var t in tuples.SelectInternal<T>(instanceType, parametlessCtor, instanceHashCode))
                {
                    yield return t;
                }
            }
            else if (paramsCtor != null)
            {
                await foreach (var record in tuples)
                {
                    yield return (T)paramsCtor.Invoke(record.Select(r => r.value == DBNull.Value ? null : r.value).ToArray());
                }
            }
            else
            {
                throw new ArgumentException();
            }
        }
    }
}