﻿/**
 *   Copyright (c) Rich Hickey. All rights reserved.
 *   The use and distribution terms for this software are covered by the
 *   Eclipse Public License 1.0 (http://opensource.org/licenses/eclipse-1.0.php)
 *   which can be found in the file epl-v10.html at the root of this distribution.
 *   By using this software in any fashion, you are agreeing to be bound by
 * 	 the terms of this license.
 *   You must not remove this notice, or any other, from this software.
 **/

/**
 *   Author: David Miller
 **/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
#if CLR2
using Microsoft.Scripting.Ast;
#else
using System.Linq.Expressions;
#endif
using Microsoft.Scripting;

using clojure.runtime;
using System.IO;

namespace clojure.lang.CljCompiler.Ast
{
    abstract class HostExpr : Expr, MaybePrimitiveExpr
    {
        #region Parsing
        
        public sealed class Parser : IParser
        {
            public Expr Parse(object frm, bool isRecurContext)
            {
                ISeq form = (ISeq)frm;

                // form is one of:
                //  (. x fieldname-sym)
                //  (. x 0-ary-method)
                //  (. x propertyname-sym)
                //  (. x methodname-sym args+)
                //  (. x (methodname-sym args?))

                if (RT.Length(form) < 3)
                    throw new ArgumentException("Malformed member expression, expecting (. target member ... )");

                string source = (string)Compiler.SOURCE.deref();
                IPersistentMap spanMap = Compiler.GetSourceSpanMap(form);

                Symbol tag = Compiler.TagOf(form);

                // determine static or instance
                // static target must be symbol, either fully.qualified.Typename or Typename that has been imported
                 
                Type t = Compiler.MaybeType(RT.second(form), false);
                // at this point, t will be non-null if static

                Expr instance = null;
                if (t == null)
                    instance = Compiler.GenerateAST(RT.second(form),false);

                bool maybeFieldOrProperty = RT.Length(form) == 3 && RT.third(form) is Symbol;

                if (maybeFieldOrProperty)
                {
                    PropertyInfo pinfo = null;
                    FieldInfo finfo = null;
                    MethodInfo minfo = null;

                    Symbol sym = (Symbol)RT.third(form);
                    string fieldName = sym.Name;
                    // The JVM version does not have to worry about Properties.  It captures 0-arity methods under fields.
                    // We have to put in special checks here for this.
                    // Also, when reflection is required, we have to capture 0-arity methods under the calls that
                    //   are generated by StaticFieldExpr and InstanceFieldExpr.
                    if (t != null)
                    {
                        if ((finfo = Reflector.GetField(t, sym.Name, true)) != null)
                            return new StaticFieldExpr(source, spanMap, tag, t, fieldName, finfo);
                        if ((pinfo = Reflector.GetProperty(t, sym.Name, true)) != null)
                            return new StaticPropertyExpr(source, spanMap, tag, t, fieldName, pinfo);
                        if ((minfo = Reflector.GetArityZeroMethod(t, fieldName, true)) != null)
                            return (MethodExpr)(new StaticMethodExpr(source, spanMap, tag, t, fieldName, PersistentVector.EMPTY));
                    }
                    else if (instance != null && instance.HasClrType && instance.ClrType != null)
                    {
                        Type instanceType = instance.ClrType;
                        if ((finfo = Reflector.GetField(instanceType, sym.Name, false)) != null)
                            return new InstanceFieldExpr(source, spanMap, tag, instance, fieldName, finfo);
                        if ((pinfo = Reflector.GetProperty(instanceType,sym.Name,false)) != null)
                            return new InstancePropertyExpr(source, spanMap, tag, instance, fieldName, pinfo);
                        if ((minfo = Reflector.GetArityZeroMethod(instanceType, fieldName, false)) != null)
                            return new InstanceMethodExpr(source, spanMap, tag, instance, fieldName, PersistentVector.EMPTY);
                    }
                    else
                    {
                        //  t is null, so we know this is not a static call
                        //  If instance is null, we are screwed anyway.
                        //  If instance is not null, then we don't have a type.
                        //  So we must be in an instance call to a property, field, or 0-arity method.
                        //  The code generated by InstanceFieldExpr/InstancePropertyExpr with a null FieldInfo/PropertyInfo
                        //     will generate code to do a runtime call to a Reflector method that will check all three.
                        return new InstanceFieldExpr(source, spanMap, tag, instance, fieldName, null); // same as InstancePropertyExpr when last arg is null
                    }
                }
 

                ISeq call = RT.third(form) is ISeq ? (ISeq)RT.third(form) : RT.next(RT.next(form));

                if (!(RT.first(call) is Symbol))
                    throw new ArgumentException("Malformed member exception");

                string methodName = ((Symbol)RT.first(call)).Name;
                IPersistentVector args = PersistentVector.EMPTY;

                for (ISeq s = RT.next(call); s != null; s = s.next())
                    args = args.cons(Compiler.GenerateAST(s.first(),false));

                return t != null
                    ? (MethodExpr)(new StaticMethodExpr(source, spanMap, tag, t, methodName, args))
                    : (MethodExpr)(new InstanceMethodExpr(source, spanMap, tag, instance, methodName, args));
            }
        }

        public abstract Expression GenDlrUnboxed(GenContext context);


        protected static List<MethodInfo> GetMethods(Type targetType, int arity,  string methodName, bool getStatics)
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.FlattenHierarchy | BindingFlags.InvokeMethod;
            flags |= getStatics ? BindingFlags.Static : BindingFlags.Instance;

            List<MethodInfo> infos;

            if (targetType.IsInterface && ! getStatics)
                infos = GetInterfaceMethods(targetType,arity,methodName);
            else
            {
                IEnumerable<MethodInfo> einfo
                    = targetType.GetMethods(flags).Where(info => info.Name == methodName && info.GetParameters().Length == arity);
                infos = new List<MethodInfo>(einfo);
            }

            return infos;
        }

        static List<MethodInfo> GetInterfaceMethods(Type targetType, int arity, string methodName)
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod;

            List<Type> interfaces = new List<Type>();
            interfaces.Add(targetType);
            interfaces.AddRange(targetType.GetInterfaces());

            List<MethodInfo> infos = new List<MethodInfo>();

            foreach ( Type type in interfaces )
            {
                MethodInfo[] methods = type.GetMethods();
                IEnumerable<MethodInfo> einfo
                     = type.GetMethods(flags).Where(info => info.Name == methodName && info.GetParameters().Length == arity);
                infos.AddRange(einfo);
            }

            return infos;
        }


        protected static MethodInfo GetMatchingMethod(IPersistentMap spanMap, Type targetType, IPersistentVector args, string methodName)
        {
            MethodInfo method = GetMatchingMethodAux(targetType, args, methodName, true);

            MaybeReflectionWarn(spanMap, method, methodName);

            return method;
        }

        protected static MethodInfo GetMatchingMethod(IPersistentMap spanMap, Expr target, IPersistentVector args, string methodName)
        {
            MethodInfo method = target.HasClrType ? GetMatchingMethodAux(target.ClrType, args, methodName, false) : null;

            MaybeReflectionWarn(spanMap, method, methodName);

            return method;
        }

        private static void MaybeReflectionWarn(IPersistentMap spanMap, MethodInfo method, string methodName)
        {
            if ( method == null && RT.booleanCast(RT.WARN_ON_REFLECTION.deref()) )
                ((TextWriter)RT.ERR.deref()).WriteLine(string.Format("Reflection warning, {0}:{1} - call to {2} can't be resolved.\n",
                    Compiler.SOURCE_PATH.deref(), spanMap == null ? (int)spanMap.valAt(RT.START_LINE_KEY, 0) : 0, methodName));
        }

        private static MethodInfo GetMatchingMethodAux(Type targetType, IPersistentVector args, string methodName, bool getStatics)
        {
            MethodInfo method = null;

            List<MethodInfo> methods = HostExpr.GetMethods(targetType, args.count(), methodName, getStatics);

            if (methods.Count == 0)
                method = null;
            else
            {

                int index = 0;
                if (methods.Count > 1)
                {
                    List<ParameterInfo[]> parms = new List<ParameterInfo[]>(methods.Count);
                    List<Type> rets = new List<Type>(methods.Count);

                    foreach (MethodInfo mi in methods)
                    {
                        parms.Add(mi.GetParameters());
                        rets.Add(mi.ReturnType);
                    }
                    index = GetMatchingParams(methodName, parms, args, rets);
                }
                method = (index >= 0 ? methods[index] : null);
            }

            return method;
        }



 
 
        internal static int GetMatchingParams(string methodName, List<ParameterInfo[]> parmlists, IPersistentVector argexprs, List<Type> rets)
        {
            // Assume matching lengths
            int matchIndex = -1;
            bool tied = false;
            bool foundExact = false;

            for (int i = 0; i < parmlists.Count; i++)
            {
                bool match = true;
                ISeq aseq = argexprs.seq();
                int exact = 0;
                for (int p = 0; match && p < argexprs.count() && aseq != null; ++p, aseq = aseq.next())
                {
                    Expr arg = (Expr)aseq.first();
                    Type atype = arg.HasClrType ? arg.ClrType : typeof(object);
                    Type ptype = parmlists[i][p].ParameterType;
                    if (arg.HasClrType && atype == ptype)
                        exact++;
                    else
                        match = Reflector.ParamArgTypeMatch(ptype, atype);
                }

                if (exact == argexprs.count())
                {
                    if ( !foundExact || matchIndex == -1 || rets[matchIndex].IsAssignableFrom(rets[i]))
                        matchIndex = i;
                    foundExact = true;
                }
                else if (match && !foundExact)
                {
                    if (matchIndex == -1)
                        matchIndex = i;
                    else
                    {
                        if (Reflector.Subsumes(parmlists[i], parmlists[matchIndex]))
                        {
                            matchIndex = i;
                            tied = false;
                        }
                        else if (Array.Equals(parmlists[i], parmlists[matchIndex]))
                            if (rets[matchIndex].IsAssignableFrom(rets[i]))
                                matchIndex = i;
                            else if (!Reflector.Subsumes(parmlists[matchIndex], parmlists[i]))
                                tied = true;
                    }
                }
            }

            if (tied)
                throw new ArgumentException("More than one matching method found: " + methodName);

            return matchIndex;
        }

        internal static Expression[] GenTypedArgs(GenContext context, ParameterInfo[] parms, IPersistentVector args)
        {
            Expression[] exprs = new Expression[parms.Length];
            for (int i = 0; i < parms.Length; i++)
                exprs[i] = GenTypedArg(context,parms[i].ParameterType, (Expr)args.nth(i));
            return exprs;
        }

        internal static Expression GenTypedArg(GenContext context, Type type, Expr arg)
        {
            if (Compiler.MaybePrimitiveType(arg) == type)
                return ((MaybePrimitiveExpr)arg).GenDlrUnboxed(context);
            else
            {
                Expression argExpr = arg.GenDlr(context);
                return GenMaybeUnboxedArg(type, argExpr);
            }
        }

        internal static readonly MethodInfo Method_Util_ConvertToSByte = typeof(Util).GetMethod("ConvertToByte");
        internal static readonly MethodInfo Method_Util_ConvertToByte = typeof(Util).GetMethod("ConvertToByte");
        internal static readonly MethodInfo Method_Util_ConvertToShort = typeof(Util).GetMethod("ConvertToShort");
        internal static readonly MethodInfo Method_Util_ConvertToUShort = typeof(Util).GetMethod("ConvertToUShort");
        internal static readonly MethodInfo Method_Util_ConvertToInt = typeof(Util).GetMethod("ConvertToInt");
        internal static readonly MethodInfo Method_Util_ConvertToUInt = typeof(Util).GetMethod("ConvertToUInt");
        internal static readonly MethodInfo Method_Util_ConvertToLong = typeof(Util).GetMethod("ConvertToLong");
        internal static readonly MethodInfo Method_Util_ConvertToULong = typeof(Util).GetMethod("ConvertToULong");
        internal static readonly MethodInfo Method_Util_ConvertToFloat = typeof(Util).GetMethod("ConvertToFloat");
        internal static readonly MethodInfo Method_Util_ConvertToDouble = typeof(Util).GetMethod("ConvertToDouble");
        internal static readonly MethodInfo Method_Util_ConvertToChar = typeof(Util).GetMethod("ConvertToChar");
        internal static readonly MethodInfo Method_Util_ConvertToDecimal = typeof(Util).GetMethod("ConvertToDecimal");

        static Expression GenMaybeUnboxedArg(Type type, Expression argExpr)
        {
            Type argType = argExpr.Type;

            if (argType == type)
                return argExpr;

            if (type.IsAssignableFrom(argType))
                return argExpr;

            if (Util.IsPrimitiveNumeric(argType) && Util.IsPrimitiveNumeric(type))
                return argExpr;

            if (type == typeof(sbyte))
                return Expression.Call(null, Method_Util_ConvertToSByte, argExpr);
            else if ( type == typeof(byte))
                return Expression.Call(null, Method_Util_ConvertToByte, argExpr);
            else if (type == typeof(short))
                return Expression.Call(null, Method_Util_ConvertToShort, argExpr);
            else if (type == typeof(ushort))
                return Expression.Call(null, Method_Util_ConvertToUShort, argExpr);
            else if (type == typeof(int))
                return Expression.Call(null, Method_Util_ConvertToInt, argExpr);
            else if (type == typeof(uint))
                return Expression.Call(null, Method_Util_ConvertToUInt, argExpr);
            else if (type == typeof(long))
                return Expression.Call(null, Method_Util_ConvertToLong, argExpr);
            else if (type == typeof(ulong))
                return Expression.Call(null, Method_Util_ConvertToULong, argExpr);
            else if (type == typeof(float))
                return Expression.Call(null, Method_Util_ConvertToFloat, argExpr);
            else if (type == typeof(double))
                return Expression.Call(null, Method_Util_ConvertToDouble, argExpr);
            else if (type == typeof(char))
                return Expression.Call(null, Method_Util_ConvertToChar, argExpr);
            else if (type == typeof(decimal))
                return Expression.Call(null, Method_Util_ConvertToDecimal, argExpr);
            
            return argExpr;
        }

        #endregion
    }
}
