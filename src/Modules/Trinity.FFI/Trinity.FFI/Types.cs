﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Trinity.Core.Lib;

namespace Trinity.FFI
{
    public enum TYPECODE : byte
    {
        /* NULL */
        NULL,
        /* Numbers */
        U8,
        I8,
        U16,
        I16,
        U32,
        I32,
        U64,
        I64,
        F32,
        F64,

        CHAR,
        STRING,
        BOOL,
        TUPLE,
        LIST,
    }

    public class TooManyFieldsInTupleException : ArgumentOutOfRangeException
    {
        public TooManyFieldsInTupleException(string paramName, string message) : base(paramName, message)
        {
        }
    }

    public class TypeCodecException : Exception
    {
        public TypeCodecException(string message) : base(message) { }
    }

    public unsafe static class TypeCodec
    {
        public static void FreeTypeCode(byte* up)
        {
            Memory.free(up);
        }

        private static List<TYPECODE> EncodeType_impl(Type T)
        {
            List<TYPECODE> code = new List<TYPECODE>();

            if(T == typeof(byte))
                code.Add(TYPECODE.U8);

            if(T == typeof(sbyte))
                code.Add(TYPECODE.I8);

            if(T == typeof(ushort))
                code.Add(TYPECODE.U16);

            if(T == typeof(short))
                code.Add(TYPECODE.I16);

            if(T == typeof(uint))
                code.Add(TYPECODE.U32);

            if(T == typeof(int))
                code.Add(TYPECODE.I32);

            if(T == typeof(ulong))
                code.Add(TYPECODE.U64);

            if(T == typeof(long))
                code.Add(TYPECODE.I64);

            if(T == typeof(float))
                code.Add(TYPECODE.F32);

            if(T == typeof(double))
                code.Add(TYPECODE.F64);

            if(T == typeof(char))
                code.Add(TYPECODE.CHAR);

            if(T == typeof(string))
                code.Add(TYPECODE.STRING);

            if(T == typeof(bool))
                code.Add(TYPECODE.BOOL);

            if(T.IsConstructedGenericType && T.GetGenericTypeDefinition() == typeof(List<>))
            {
                code.Add(TYPECODE.LIST);
                code.AddRange(EncodeType_impl(T.GetGenericArguments().First()));
            }

            //  Only scan the type as a struct/class, when:
            //  
            //   1. We are not detecting the type as anything else.
            //   This prevents accidental scanning of Lists, string, etc.
            //   2. It is not abstract. We do not support abstract classes.
            //   3. It is either a class, or a value type that is not primitive,
            //   and not a enum (which means that it is really a struct).
            if(code.Count == 0 && !T.IsAbstract && (T.IsClass || (T.IsValueType && !T.IsPrimitive && !T.IsEnum)))
            {
                code.Add(TYPECODE.TUPLE);
                List<List<TYPECODE>> fields = new List<List<TYPECODE>>();
                foreach(var field in T.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    fields.Add(EncodeType_impl(field.FieldType));
                }
                if(fields.Count > 255)
                    throw new TooManyFieldsInTupleException(nameof(T), $"Too many fields in Tuple type {T.ToString()}");
                code.Add((TYPECODE)fields.Count);
                foreach(var field in fields)
                {
                    code.AddRange(field);
                }
            }

            // Throw if we don't pick up anything
            if(code.Count == 0)
                throw new TypeCodecException($"Cannot encode the specified type {T.ToString()}");

            return code;
        }

        public static byte* EncodeType<T>()
        {
            return EncodeType(typeof(T));
        }

        public static byte* EncodeType(Type T)
        {
            List<TYPECODE> code = EncodeType_impl(T);
            fixed(TYPECODE* bp = code.ToArray())
            {
                byte* up = (byte*)Memory.malloc((byte)code.Count);
                Memory.memcpy(up, bp, (ulong)code.Count);
                return up;
            }
        }

        private static Type DecodeType_impl(ref TYPECODE* p)
        {
            switch (*p++)
            {
                case TYPECODE.BOOL:
                    return typeof(bool);
                case TYPECODE.CHAR:
                    return typeof(char);
                case TYPECODE.STRING:
                    return typeof(string);
                case TYPECODE.F32:
                    return typeof(float);
                case TYPECODE.F64:
                    return typeof(double);
                case TYPECODE.I8:
                    return typeof(sbyte);
                case TYPECODE.U8:
                    return typeof(byte);
                case TYPECODE.I16:
                    return typeof(short);
                case TYPECODE.U16:
                    return typeof(ushort);
                case TYPECODE.I32:
                    return typeof(int);
                case TYPECODE.U32:
                    return typeof(uint);
                case TYPECODE.I64:
                    return typeof(long);
                case TYPECODE.U64:
                    return typeof(ulong);
                case TYPECODE.LIST:
                    return typeof(List<>).MakeGenericType(DecodeType_impl(ref p));
                case TYPECODE.TUPLE:
                    {
                        byte cnt = (byte)*(p++);
                        Type[] elements = new Type[cnt];
                        for (int i = 0; i < cnt; ++i)
                        {
                            elements[i] = DecodeType_impl(ref p);
                        }
                        return BuildValueTuple(elements);
                    }
                default:
                    throw new TypeCodecException("Cannot decode type");
            }
        }

        private static Type BuildValueTuple(Type[] elements, int i = 0)
        {
            int len = elements.Length - i;
            if (len == 0)
                throw new TypeCodecException("At least one type required to construct a ValueTuple");

            switch(len)
            {
                case 1:
                    return typeof(ValueTuple<>).MakeGenericType(elements[i]);
                case 2:
                    return typeof(ValueTuple<,>).MakeGenericType(elements[i], elements[i+1]);
                case 3:
                    return typeof(ValueTuple<,,>).MakeGenericType(elements[i], elements[i+1], elements[i+2]);
                case 4:
                    return typeof(ValueTuple<,,,>).MakeGenericType(elements[i], elements[i+1], elements[i+2], elements[i+3]);
                case 5:
                    return typeof(ValueTuple<,,,,>).MakeGenericType(elements[i], elements[i+1], elements[i+2], elements[i+3], elements[i+4]);
                case 6:
                    return typeof(ValueTuple<,,,,,>).MakeGenericType(elements[i], elements[i+1], elements[i+2], elements[i+3], elements[i+4], elements[i+5]);
                case 7:
                    return typeof(ValueTuple<,,,,,,>).MakeGenericType(elements[i], elements[i+1], elements[i+2], elements[i+3], elements[i+4], elements[i+5], elements[i+6]);
            }
            // Got to chain TRest

            Debug.Assert(i == 0);
            int j = len - len % 7;
            Type t = BuildValueTuple(elements, j);
            do
            {
                j = j - 7;
                t = typeof(ValueTuple<,,,,,,,>).MakeGenericType(elements[j], elements[j + 1], elements[j + 2], elements[j + 3], elements[j + 4], elements[j + 5], elements[j + 6], t);
            } while (j > 0);

            return t;
        }

        public static Type DecodeType(void* T)
        {
            TYPECODE* p = (TYPECODE*)T;
            return DecodeType_impl(ref p);
        }
    }
}
