﻿module IL
    open System
    open System.Reflection
    open System.Reflection.Emit
    open Errors

    type instruction = 
        | Add 
        | Call         of System.Reflection.MethodInfo
        | Callvirt     of System.Reflection.MethodInfo
        | DeclareLocal of System.Type
        | Div
        | Ldc_I4       of int
        | Ldc_I4_0
        | Ldloc_0
        | Ldloc_1
        | Ldloca_s     of byte
        | Mul
        | Neg
        | Newobj       of System.Reflection.ConstructorInfo
        | Nop
        | Pop
        | Refanyval
        | Ret
        | Stloc_0
        | Stloc_1
        | Stloc         of byte
        | Sub

    type Method = { Instructions: instruction list; Locals: string list } 
     
    let emit (ilg : Emit.ILGenerator) inst = 
        match inst with 
        | Add            -> ilg.Emit(OpCodes.Add)
        | Call mi        -> ilg.Emit(OpCodes.Call, mi)
        | Callvirt mi    -> ilg.Emit(OpCodes.Callvirt, mi)
        | DeclareLocal t -> ignore(ilg.DeclareLocal(t))
        | Div            -> ilg.Emit(OpCodes.Div)
        | Ldc_I4   n     -> ilg.Emit(OpCodes.Ldc_I4, n)
        | Ldc_I4_0       -> ilg.Emit(OpCodes.Ldc_I4_0)
        | Ldloc_0        -> ilg.Emit(OpCodes.Ldloc_0)
        | Ldloc_1        -> ilg.Emit(OpCodes.Ldloc_1)
        | Ldloca_s b     -> ilg.Emit(OpCodes.Ldloca_S, b)
        | Mul            -> ilg.Emit(OpCodes.Mul)
        | Neg            -> ilg.Emit(OpCodes.Neg)
        | Newobj ci      -> ilg.Emit(OpCodes.Newobj, ci)
        | Nop            -> ilg.Emit(OpCodes.Nop)
        | Pop            -> ilg.Emit(OpCodes.Pop)
        | Refanyval      -> ilg.Emit(OpCodes.Refanyval)
        | Ret            -> ilg.Emit(OpCodes.Ret)
        | Stloc_0        -> ilg.Emit(OpCodes.Stloc_0)
        | Stloc_1        -> ilg.Emit(OpCodes.Stloc_1)
        | Stloc n        -> ilg.Emit(OpCodes.Stloc, n)
        | Sub            -> ilg.Emit(OpCodes.Sub)

    let compileEntryPoint (moduleContainingMethod : System.Reflection.Emit.ModuleBuilder) (methodToCall: System.Reflection.Emit.MethodBuilder) = 
        let mb = 
            let tb = 
                let className = "Program"
                let ta = TypeAttributes.NotPublic ||| TypeAttributes.AutoLayout ||| TypeAttributes.AnsiClass ||| TypeAttributes.BeforeFieldInit
                moduleContainingMethod.DefineType(className, ta)
            let ma = MethodAttributes.Public ||| MethodAttributes.Static 
            let methodName = "Main"
            tb.DefineMethod(methodName, ma)
        let ilg = mb.GetILGenerator() |> emit
        let ci = methodToCall.ReflectedType.GetConstructor([||])
        ilg (Newobj ci)
        ilg (Call methodToCall)
        if methodToCall.ReturnType <> null then
            ilg (DeclareLocal methodToCall.ReturnType)
            ilg Stloc_0
            ilg (Ldloca_s 0uy)
            let mi = methodToCall.ReturnType.GetMethod("ToString", [||])
            ilg (Call mi)
            let writeln = typeof<System.Console>.GetMethod("WriteLine", [| typeof<System.String> |])
            ilg (Call writeln)
        ilg Ret
        mb

    let private methodName = "MethodName"

    let compileMethod(moduleName: string) (instructions: seq<instruction>) (methodResultType) =
        let ab = 
            let assemName = System.IO.Path.ChangeExtension(moduleName, ".exe")
            let an = new AssemblyName(assemName)
            AppDomain.CurrentDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.RunAndSave)
        let modb = ab.DefineDynamicModule(moduleName)
        let tb = 
            let className = "CompiledCode"
            let ta = TypeAttributes.Public ||| TypeAttributes.AutoLayout ||| TypeAttributes.AnsiClass ||| TypeAttributes.BeforeFieldInit
            modb.DefineType(className, ta)
        let mb = 
            let ma = MethodAttributes.Public ||| MethodAttributes.HideBySig
            tb.DefineMethod(methodName, ma, methodResultType, System.Type.EmptyTypes)
        let ilg = mb.GetILGenerator() |> emit
        for instruction in instructions do
            ilg instruction
        ilg Ret
        
        let t = tb.CreateType()
        let ep = compileEntryPoint modb mb
        ab.SetEntryPoint(ep, PEFileKinds.ConsoleApplication)
        modb.CreateGlobalFunctions()
        (t, ab)

    let toMethod(methodWithInstructions, resultType) =
        match methodWithInstructions with 
            | Success m -> 
                let moduleName = "test.exe"
                let (t, ab) = compileMethod moduleName m.Instructions resultType
                Success(t.GetMethod(methodName))
            | Error e -> Error(e)

    let execute<'TMethodResultType> (instructions, saveAs) =
        let moduleName = match saveAs with
                         | Some s -> s
                         | None   -> "test.exe"
        let (t, ab) = compileMethod moduleName instructions typeof<'TMethodResultType>
        if saveAs.IsSome then 
            ab.Save(t.Module.ScopeName)
        let instance = Activator.CreateInstance(t)
        t.GetMethod(methodName).Invoke(instance, null) :?> 'TMethodResultType

    let print (instructions: seq<instruction>) =
        let p = sprintf "%A"
        Seq.map p instructions        