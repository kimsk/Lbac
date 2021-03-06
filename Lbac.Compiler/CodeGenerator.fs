﻿module CodeGenerator

    open Errors
    open IL
    open Syntax

    let private local_var_index locals name = 
        List.tryFindIndex (fun l -> System.String.Equals(l, name, System.StringComparison.Ordinal)) locals

    let private codegen_assign locals name =
        match local_var_index locals name with
            | None -> Error ("Undeclared variable " + name)
            | Some i -> 
                match i with 
                | 0 -> Success(Stloc_0)
                | 1 -> Success(Stloc_1)
                | _ -> Success(Stloc (System.Convert.ToByte(i)))

    let private codegen_oper = function
        | Add -> instruction.Add
        | Subtract -> instruction.Sub
        | Multiply -> instruction.Mul
        | Divide -> instruction.Div

    let private tryLdLoc ((locals : string list), (name : string)) = 
        match local_var_index locals name with
            | None -> Error ("Undeclared variable " + name)
            | Some i -> 
                match i with 
                | 0 -> Success(Ldloc_0)
                | 1 -> Success(Ldloc_1)
                | _ -> Success(Ldloca_s (System.Convert.ToByte(i)))

    let rec codegenExpr (acc : Method) (expr : Expr) = 
        match expr with
        | Variable v -> 
            match tryLdLoc (acc.Locals, v) with 
            | Success inst -> Success({ acc with Instructions = acc.Instructions @ [inst] })
            | Error   err  -> Error err
        | Invoke m -> Error "Sorry; no can do"
        | Minus e -> 
            match codegenExpr acc e with
            | Success m -> Success({ m with Instructions = m.Instructions @ [Neg] })
            | err -> err
        | Number n -> 
            match n with
            | 0 -> Success({ acc with Instructions = acc.Instructions @ [Ldc_I4_0] })
            | _ -> Success({ acc with Instructions = acc.Instructions @ [Ldc_I4 n] })
        | Assign (n, rhs) -> 
            let rhsMethod = codegenExpr { acc with Instructions = [] } rhs
            match (n, rhsMethod) with 
            | (Variable name, Success r) -> 
                match codegen_assign r.Locals name with 
                | Success assign_inst -> 
                    let insts = List.concat [ r.Instructions; [ assign_inst ] ]
                    Success({ Instructions = acc.Instructions @ insts; Locals = r.Locals })
                | Error a -> Error(a)
            | (_, Success r) -> failwith "A variable is required on the left hand side of an assignment." // Should never happen; parser should not emit this
            | (_, Error r) -> rhsMethod
        | Binary (lhs, oper, rhs) -> 
            let lhsMethod = codegenExpr { acc with Instructions = [] } lhs
            let rhsMethod = codegenExpr { acc with Instructions = [] } rhs
            let operInst = codegen_oper oper
            match (lhsMethod, rhsMethod) with
                | (Success l, Success r) -> 
                    let insts       = List.concat [ l.Instructions; r.Instructions; [operInst] ]
                    let mergeLocals = List.concat [ l.Locals; List.filter (fun i2 -> not (List.exists (fun i1 -> i1 = i2) l.Locals)) r.Locals]
                    Success({ Instructions = acc.Instructions @ insts; Locals = mergeLocals })
                | (Error l, _) -> lhsMethod
                | (_, Error r) -> rhsMethod

    let rec codegen (parsed : ParseResult) =
        let locals = 
            parsed.Locals 
            |> List.ofSeq 
        let tryCodeGenLine acc line = 
            match acc, line with
            | Success accMethod, Success expr -> codegenExpr accMethod expr
            | _, Error err -> Error err
            | Error err, _ -> Error err
        let localDeclarations = [for name in locals -> DeclareLocal(typedefof<int>)]
        let emptyMethod = Success( { Instructions = localDeclarations; Locals = locals } )
        List.fold tryCodeGenLine emptyMethod parsed.Lines
