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

#if CLR2
using Microsoft.Scripting.Ast;
#else
using System.Linq.Expressions;
#endif

namespace clojure.lang.CljCompiler.Ast
{
    class FnMethod : ObjMethod
    {
        #region Data
        
        protected IPersistentVector _reqParms = PersistentVector.EMPTY;  // localbinding => localbinding
        protected LocalBinding _restParm = null;
        Type[] _argTypes;
        Type _retType;

        #endregion

        #region C-tors

        public FnMethod(FnExpr fn, ObjMethod parent)
            :base(fn,parent)
        {
        }

        // For top-level compilation only
        public FnMethod(FnExpr fn, ObjMethod parent, BodyExpr body)
            :base(fn,parent)
        {
            _body = body;
            _argLocals = PersistentVector.EMPTY;
            //_thisBinding = Compiler.RegisterLocal(Symbol.intern(fn.ThisName ?? "fn__" + RT.nextID()), null, null, false);
        }

        #endregion

        #region ObjMethod methods

        internal override bool IsVariadic
        {
            get { return _restParm != null; }
        }

        internal override int NumParams
        {
            get { return _reqParms.count() + (IsVariadic ? 1 : 0); }
        }

        internal override int RequiredArity
        {
            get { return _reqParms.count(); }
        } 

        internal override string MethodName
        {
            get { return IsVariadic ? "doInvoke" : "invoke"; }
        }

        protected override string StaticMethodName
        {
            get
            {
                if (Objx.IsStatic && Compiler.IsCompiling)
                    return "InvokeStatic";
                return String.Format("__invokeHelper_{0}{1}", RequiredArity, IsVariadic ? "v" : string.Empty);
            }
        }

        protected override Type[] StaticMethodArgTypes
        {
            get
            {
                if (_argTypes != null)
                    return _argTypes;

                return ArgTypes;
            }
        }

        protected override Type[] ArgTypes
        {
            get
            {
                if (IsVariadic && _reqParms.count() == Compiler.MAX_POSITIONAL_ARITY)
                {
                    Type[] ret = new Type[Compiler.MAX_POSITIONAL_ARITY + 1];
                    for (int i = 0; i < Compiler.MAX_POSITIONAL_ARITY + 1; i++)
                        ret[i] = typeof(Object);
                    return ret;
                }

                return Compiler.CreateObjectTypeArray(NumParams);
            }
        }

        protected override Type ReturnType
        {
            get { return typeof(object); }
        }

        protected override Type StaticReturnType
        {
            get
            {
                if (Objx.IsStatic)
                    return _retType;

                return typeof(object);
            }
        }

        #endregion

        #region Parsing

        enum ParamParseState { Required, Rest, Done };

        internal static FnMethod Parse(FnExpr fn, ISeq form, bool isStatic)
        {
            // ([args] body ... )

            IPersistentVector parms = (IPersistentVector)RT.first(form);
            ISeq body = RT.next(form);

            try
            {
                FnMethod method = new FnMethod(fn, (ObjMethod)Compiler.METHOD.deref());

                Var.pushThreadBindings(RT.map(
                    Compiler.METHOD, method,
                    Compiler.LOCAL_ENV, Compiler.LOCAL_ENV.deref(),
                    Compiler.LOOP_LOCALS, null,
                    Compiler.NEXT_LOCAL_NUM, 0));

                method._retType = Compiler.TagType(Compiler.TagOf(parms));
                if (method._retType.IsPrimitive && !(method._retType == typeof(double) || method._retType == typeof(long)))
                    throw new ArgumentException("Only long and double primitives are supported");

                // register 'this' as local 0  
                if ( !isStatic )
                    method._thisBinding = Compiler.RegisterLocal(Symbol.intern(fn.ThisName ?? "fn__" + RT.nextID()), null, null,false);

                ParamParseState paramState = ParamParseState.Required;
                IPersistentVector argLocals = PersistentVector.EMPTY;
                List<Type> argTypes = new List<Type>();

                int parmsCount = parms.count();

                for (int i = 0; i < parmsCount; i++)
                {
                    if (!(parms.nth(i) is Symbol))
                        throw new ArgumentException("fn params must be Symbols");
                    Symbol p = (Symbol)parms.nth(i);
                    if (p.Namespace != null)
                        throw new Exception("Can't use qualified name as parameter: " + p);
                    if (p.Equals(Compiler._AMP_))
                    {
                        //if (isStatic)
                        //    throw new Exception("Variadic fns cannot be static");

                        if (paramState == ParamParseState.Required)
                            paramState = ParamParseState.Rest;
                        else
                            throw new Exception("Invalid parameter list");
                    }
                    else
                    {
                        Type pt = Compiler.TagType(Compiler.TagOf(p));
                        if (pt.IsPrimitive && !isStatic)
                        {
                            pt = typeof(object);
                            p = (Symbol)((IObj)p).withMeta((IPersistentMap)RT.assoc(RT.meta(p), RT.TAG_KEY, null));
                            //throw new Exception("Non-static fn can't have primitive parameter: " + p);
                        }
                        if (pt.IsPrimitive && !(pt == typeof(double) || pt == typeof(long)))
                            throw new ArgumentException("Only long and double primitives are supported: " + p);

                        if (paramState == ParamParseState.Rest && Compiler.TagOf(p) != null)
                            throw new Exception("& arg cannot have type hint");
                        if (paramState == ParamParseState.Rest)
                            pt = typeof(ISeq);
                        argTypes.Add(pt);
                        LocalBinding b = isStatic
                            ? Compiler.RegisterLocal(p,null, new MethodParamExpr(pt), true)
                            : Compiler.RegisterLocal(p,
                            paramState == ParamParseState.Rest ? Compiler.ISEQ : Compiler.TagOf(p),
                            null,true);

                        argLocals = argLocals.cons(b);
                        switch (paramState)
                        {
                            case ParamParseState.Required:
                                method._reqParms = method._reqParms.cons(b);
                                break;
                            case ParamParseState.Rest:
                                method._restParm = b;
                                paramState = ParamParseState.Done;
                                break;
                            default:
                                throw new Exception("Unexpected parameter");
                        }
                    }
                }

                if (method.NumParams > Compiler.MAX_POSITIONAL_ARITY)
                    throw new Exception(string.Format("Can't specify more than {0} parameters", Compiler.MAX_POSITIONAL_ARITY));
                Compiler.LOOP_LOCALS.set(argLocals);
                method._argLocals = argLocals;
                if (isStatic)
                    method._argTypes = argTypes.ToArray();
                method._body = (new BodyExpr.Parser()).Parse(new ParserContext(RHC.Return),body);
                return method;
            }
            finally
            {
                Var.popThreadBindings();
            }
        }

        #endregion

        #region Immediate mode compilation

        internal LambdaExpression GenerateImmediateLambda(RHC rhc, ObjExpr objx, GenContext context)
        {
            List<ParameterExpression> parmExprs = new List<ParameterExpression>(_argLocals.count());

            if (_thisBinding != null )
                _thisBinding.ParamExpression = objx.ThisParam;

            try
            {

                LabelTarget loopLabel = Expression.Label("top");

                Var.pushThreadBindings(RT.map(Compiler.LOOP_LABEL, loopLabel, Compiler.METHOD, this));

                for (int i = 0; i < _argLocals.count(); i++)
                {
                    LocalBinding b = (LocalBinding)_argLocals.nth(i);

                    ParameterExpression pexpr = Expression.Parameter(typeof(object), b.Name);
                    b.ParamExpression = pexpr;
                    parmExprs.Add(pexpr);
                }

                List<Expression> bodyExprs = new List<Expression>();
                //bodyExprs.AddRange(typedParmInitExprs);
                bodyExprs.Add(Expression.Label(loopLabel));
                bodyExprs.Add(Compiler.MaybeBox(_body.GenCode(rhc,objx,context)));


                Expression block;
                //if (typedParmExprs.Count > 0)
                //    block = Expression.Block(typedParmExprs, bodyExprs);
                //else
                block = Expression.Block(bodyExprs);

                return Expression.Lambda(
                    FuncTypeHelpers.GetFFuncType(parmExprs.Count),
                    block,
                    Objx.ThisName,
                    parmExprs);
            }
            finally
            {
                Var.popThreadBindings();
            }
        }

        #endregion

    }
}
