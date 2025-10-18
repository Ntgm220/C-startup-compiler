using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.Globalization;
public class EvalVisitor : RedLangBaseVisitor<EvalVisitor.Value>
{
    // ===== I/O hooks =====
    public Action<string>? Output;
    public Func<string, string>? Input;


    //===== Value union =====
    public enum ValType { Int, Float, Bool, String, Null }

    public readonly struct Value
    {
        public readonly ValType Type;
        public readonly double Num;     // Int/Float/Bool (0/1)
        public readonly string? Str;    // String
        private Value(ValType t, double n, string? s) { Type = t; Num = n; Str = s; }

        public static Value FromInt(long i) => new(ValType.Int, i, null);
        public static Value FromFloat(double d) => new(ValType.Float, d, null);
        public static Value FromBool(bool b) => new(ValType.Bool, b ? 1 : 0, null);
        public static Value FromString(string s) => new(ValType.String, 0, s);
        public static Value Null() => new(ValType.Null, 0, null);

        public bool AsBool() => Type == ValType.String ? !string.IsNullOrEmpty(Str) : Num != 0;
        public double AsNumber() => Type == ValType.String ? double.Parse(Str ?? "0", CultureInfo.InvariantCulture) : Num;
        public string AsString() => Type switch
        {
            ValType.String => Str ?? "",
            ValType.Bool => Num != 0 ? "true" : "false",
            ValType.Int => ((long)Num).ToString(CultureInfo.InvariantCulture),
            ValType.Float => Num.ToString(CultureInfo.InvariantCulture),
            _ => ""
        };
        public override string ToString() => AsString();
    }

    // ===== Símbolos y funciones =====
    private sealed class Var { public string Type = "float"; public Value Val = Value.FromFloat(0); }
    private sealed class FuncDef
    {
        public string Name = "";
        public (string name, string type)[] Params = Array.Empty<(string, string)>();
        public string ReturnType = "float"; // tu grammar exige ': type'
        public RedLang.BlockContext Body = default!;
    }

    private readonly Stack<Dictionary<string, Var>> _scopes = new();
    private readonly Dictionary<string, FuncDef> _funcs = new();
    private int _funcDepth = 0;

    public EvalVisitor() { _scopes.Push(new()); } // global

    // ===== Helpers =====
    private Var? ResolveVar(string name)
    {
        foreach (var scope in _scopes)
            if (scope.TryGetValue(name, out var v)) return v;
        return null;
    }

    private void Declare(string name, string type, Value? init = null)
    {
        var v = new Var { Type = type, Val = Coerce(init ?? DefaultOf(type), type) };
        _scopes.Peek()[name] = v;
    }

    private static Value DefaultOf(string type) => type switch
    {
        "int" => Value.FromInt(0),
        "float" => Value.FromFloat(0),
        "bool" => Value.FromBool(false),
        "s" => Value.FromString(""),
        _ => Value.Null()
    };

    private static Value Coerce(Value v, string type) => type switch
    {
        "int" => Value.FromInt((long)Math.Truncate(v.AsNumber())),
        "float" => Value.FromFloat(v.AsNumber()),
        "bool" => Value.FromBool(v.AsBool()),
        "s" => Value.FromString(v.AsString()),
        _ => v
    };

    private static bool IsString(Value v) => v.Type == ValType.String;

    private static Value Add(Value a, Value b)
    {
        if (IsString(a) || IsString(b)) return Value.FromString(a.AsString() + b.AsString());
        if (a.Type == ValType.Float || b.Type == ValType.Float) return Value.FromFloat(a.AsNumber() + b.AsNumber());
        return Value.FromInt((long)(a.AsNumber() + b.AsNumber()));
    }
    private static Value Sub(Value a, Value b)
    {
        if (IsString(a) || IsString(b)) throw new Exception("No se puede restar strings.");
        var r = a.AsNumber() - b.AsNumber();
        if (a.Type == ValType.Float || b.Type == ValType.Float) return Value.FromFloat(r);
        return Value.FromInt((long)r);
    }
    private static Value Mul(Value a, Value b)
    {
        if (IsString(a) || IsString(b)) throw new Exception("No se puede multiplicar strings.");
        var r = a.AsNumber() * b.AsNumber();
        if (a.Type == ValType.Float || b.Type == ValType.Float) return Value.FromFloat(r);
        return Value.FromInt((long)r);
    }
    private static Value Div(Value a, Value b)
    {
        if (IsString(a) || IsString(b)) throw new Exception("No se puede dividir strings.");
        return Value.FromFloat(a.AsNumber() / b.AsNumber());
    }
    private static Value Mod(Value a, Value b)
    {
        if (IsString(a) || IsString(b)) throw new Exception("No se puede hacer % con strings.");
        return Value.FromFloat(a.AsNumber() % b.AsNumber());
    }

    private static Value Cmp(Value a, Value b, Func<double, double, bool> pred)
    {
        if (IsString(a) || IsString(b)) throw new Exception(">, <, >=, <= requieren números.");
        return Value.FromBool(pred(a.AsNumber(), b.AsNumber()));
    }
    private static Value Eq(Value a, Value b)
    {
        if (IsString(a) || IsString(b)) return Value.FromBool(a.AsString() == b.AsString());
        return Value.FromBool(Math.Abs(a.AsNumber() - b.AsNumber()) == 0);
    }
    private static Value Neq(Value a, Value b)
    {
        if (IsString(a) || IsString(b)) return Value.FromBool(a.AsString() != b.AsString());
        return Value.FromBool(Math.Abs(a.AsNumber() - b.AsNumber()) != 0);
    }

    private static string Unescape(string s)
    {
        var r = new System.Text.StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c != '\\') { r.Append(c); continue; }
            if (++i >= s.Length) { r.Append('\\'); break; }
            var n = s[i];
            r.Append(n switch { '"' => '"', '\\' => '\\', 'n' => '\n', 'r' => '\r', 't' => '\t', _ => n });
        }
        return r.ToString();
    }

    private sealed class ReturnSignal : Exception { public readonly Value Ret; public ReturnSignal(Value v) { Ret = v; } }

    // ===== program / block =====
    public override Value VisitProgram(RedLang.ProgramContext ctx)
    {
        Value last = Value.Null();
        foreach (var st in ctx.statement()) last = Visit(st);
        return last;
    }

    public override Value VisitBlock(RedLang.BlockContext ctx)
    {
        _scopes.Push(new());
        try
        {
            Value last = Value.Null();
            foreach (var st in ctx.statement()) last = Visit(st);
            return last;
        }
        finally { _scopes.Pop(); }
    }

    // ===== declaraciones y asignaciones =====
    public override Value VisitVarDef(RedLang.VarDefContext ctx)
    {
        var name = ctx.IDENTIFIER().GetText();
        var type = ctx.type().GetText();
        var init = ctx.expression() != null ? Visit(ctx.expression()) : DefaultOf(type);
        Declare(name, type, init);
        return Value.Null(); // << no eco " = 0"
    }

    public override Value VisitAssignment(RedLang.AssignmentContext ctx)
    {
        var name = ctx.IDENTIFIER().GetText();
        var v = ResolveVar(name) ?? throw new Exception($"La variable '{name}' no está declarada.");
        var rhs = Visit(ctx.expression());
        v.Val = Coerce(rhs, v.Type);
        return Value.Null(); // << no eco tras 'set'
    }

    public override Value VisitFunctionDef(RedLang.FunctionDefContext ctx)
    {
        var name = ctx.IDENTIFIER().GetText();

        var paramList = new List<(string, string)>();
        if (ctx.paramList() != null)
            foreach (var p in ctx.paramList().param())
                paramList.Add((p.IDENTIFIER().GetText(), p.type().GetText()));

        var retType = ctx.type().GetText();

        // ✅ Registrar primero (permite recursion)
        var f = new FuncDef
        {
            Name = name,
            Params = paramList.ToArray(),
            ReturnType = retType,
            Body = ctx.block()
        };

        _funcs[name] = f;
        return Value.Null();
    }


    public override Value VisitFunctionCall(RedLang.FunctionCallContext ctx)
    {
        var name = ctx.IDENTIFIER().GetText();
        if (!_funcs.TryGetValue(name, out var f))
            throw new Exception($"La función '{name}' no está definida.");

        var args = new List<Value>();
        if (ctx.argList() != null)
            foreach (var e in ctx.argList().expression())
                args.Add(Visit(e));

        if (args.Count != f.Params.Length)
            throw new Exception($"La función '{name}' requiere {f.Params.Length} argumento(s).");

        _scopes.Push(new());
        _funcDepth++;

        try
        {
            for (int i = 0; i < f.Params.Length; i++)
            {
                var (pname, ptype) = f.Params[i];
                Declare(pname, ptype, Coerce(args[i], ptype));
            }

            Visit(f.Body);
            return DefaultOf(f.ReturnType);
        }
        catch (ReturnSignal r)
        {
            return Coerce(r.Ret, f.ReturnType);
        }
        finally
        {
            _funcDepth--;
            _scopes.Pop();
        }
    }



    public override Value VisitFunctionCallStmt(RedLang.FunctionCallStmtContext ctx)
    {
        Visit(ctx.functionCall());      // ejecuta efectos
        return Value.Null();            // << no eco
    }

    public override Value VisitGiveStmt(RedLang.GiveStmtContext ctx)
    {
        var v = Visit(ctx.expression());
        if (_funcDepth > 0) throw new ReturnSignal(v);
        return Value.Null(); // << como sentencia suelta, no eco
    }

    // ===== control de flujo =====
    public override Value VisitIfStmt(RedLang.IfStmtContext ctx)
    {
        var cond = Visit(ctx.expression()).AsBool();
        if (cond) { Visit(ctx.block(0)); return Value.Null(); }
        if (ctx.OTHERWISE() != null) { Visit(ctx.block(1)); return Value.Null(); }
        return Value.Null();
    }

    public override Value VisitLoopStmt(RedLang.LoopStmtContext ctx)
    {
        // forma for(init; cond; step) { body }
        var init = ctx.statement(0);
        var step = ctx.statement(1);

        Visit(init);
        int guard = 0, max = 1_000_000;
        while (Visit(ctx.expression()).AsBool())
        {
            Visit(ctx.block());
            Visit(step);
            if (++guard > max) throw new Exception("Se excedió el límite de iteraciones del bucle.");
        }
        return Value.Null();
    }

    public override Value VisitShowStmt(RedLang.ShowStmtContext ctx)
    {
        var v = Visit(ctx.expression());
        (Output ?? Console.WriteLine)(v.AsString());
        return Value.Null(); // << evita "= valor" después de imprimir
    }

    public override Value VisitAskStmt(RedLang.AskStmtContext ctx)
    {
        var name = ctx.IDENTIFIER().GetText();
        var prompt = $"{name}: ";

        string text;
        if (Input != null) text = Input(prompt);
        else if (Console.IsInputRedirected) text = "";
        else { Console.Write(prompt); text = Console.ReadLine() ?? ""; }

        var varRef = ResolveVar(name);
        if (varRef == null)
        {
            Declare(name, "s", Value.FromString(text));
            return Value.Null(); // << no eco
        }

        var t = varRef.Type;
        var trimmed = (text ?? "").Trim();
        Value parsed = t switch
        {
            "int" => Value.FromInt(long.Parse(trimmed.Length == 0 ? "0" : trimmed, CultureInfo.InvariantCulture)),
            "float" => Value.FromFloat(double.Parse(trimmed.Length == 0 ? "0" : trimmed, CultureInfo.InvariantCulture)),
            "bool" => Value.FromBool(trimmed.Equals("true", StringComparison.OrdinalIgnoreCase) || trimmed == "1"),
            "s" => Value.FromString(text ?? ""),
            _ => Value.FromString(text ?? "")
        };
        varRef.Val = Coerce(parsed, t);
        return Value.Null(); // << no eco
    }

    // ===== expresiones =====
    public override Value VisitExpression(RedLang.ExpressionContext ctx) => Visit(ctx.logicOr());

    // Solo booleano si hay '||'
    public override Value VisitLogicOr(RedLang.LogicOrContext ctx)
    {
        if (ctx.OR() == null || ctx.OR().Length == 0)
            return Visit(ctx.logicAnd(0));

        var acc = Visit(ctx.logicAnd(0)).AsBool();
        for (int i = 1; i < ctx.logicAnd().Length; i++)
        {
            if (acc) return Value.FromBool(true);
            acc = Visit(ctx.logicAnd(i)).AsBool();
        }
        return Value.FromBool(acc);
    }

    // Solo booleano si hay '&&'
    public override Value VisitLogicAnd(RedLang.LogicAndContext ctx)
    {
        if (ctx.AND() == null || ctx.AND().Length == 0)
            return Visit(ctx.equality(0));

        var acc = Visit(ctx.equality(0)).AsBool();
        for (int i = 1; i < ctx.equality().Length; i++)
        {
            if (!acc) return Value.FromBool(false);
            acc = Visit(ctx.equality(i)).AsBool();
        }
        return Value.FromBool(acc);
    }

    public override Value VisitEquality(RedLang.EqualityContext ctx)
    {
        var val = Visit(ctx.comparison(0));
        for (int i = 1; i < ctx.comparison().Length; i++)
        {
            var op = ctx.GetChild(2 * i - 1).GetText();
            var rhs = Visit(ctx.comparison(i));
            val = op == "==" ? Eq(val, rhs) : Neq(val, rhs);
        }
        return val;
    }

    public override Value VisitComparison(RedLang.ComparisonContext ctx)
    {
        var val = Visit(ctx.term(0));
        for (int i = 1; i < ctx.term().Length; i++)
        {
            var op = ctx.GetChild(2 * i - 1).GetText();
            var rhs = Visit(ctx.term(i));
            val = op switch
            {
                ">" => Cmp(val, rhs, (a, b) => a > b),
                "<" => Cmp(val, rhs, (a, b) => a < b),
                ">=" => Cmp(val, rhs, (a, b) => a >= b),
                "<=" => Cmp(val, rhs, (a, b) => a <= b),
                _ => throw new Exception($"Operador de comparación desconocido '{op}'")
            };
        }
        return val;
    }

    public override Value VisitTerm(RedLang.TermContext ctx)
    {
        var val = Visit(ctx.factor(0));
        for (int i = 1; i < ctx.factor().Length; i++)
        {
            var op = ctx.GetChild(2 * i - 1).GetText();
            var rhs = Visit(ctx.factor(i));
            val = op == "+" ? Add(val, rhs) : Sub(val, rhs);
        }
        return val;
    }

    public override Value VisitFactor(RedLang.FactorContext ctx)
    {
        var val = Visit(ctx.unary(0));
        for (int i = 1; i < ctx.unary().Length; i++)
        {
            var op = ctx.GetChild(2 * i - 1).GetText();
            var rhs = Visit(ctx.unary(i));
            val = op switch
            {
                "*" => Mul(val, rhs),
                "/" => Div(val, rhs),
                "%" => Mod(val, rhs),
                _ => throw new Exception($"Operador desconocido '{op}'")
            };
        }
        return val;
    }

    public override Value VisitUnary(RedLang.UnaryContext ctx)
    {
        var prim = Visit(ctx.primary());
        if (ctx.MINUS() != null)
        {
            if (IsString(prim)) throw new Exception("No se puede negar un string.");
            return prim.Type == ValType.Int ? Value.FromInt((long)(-prim.AsNumber()))
                                          : Value.FromFloat(-prim.AsNumber());
        }
        if (ctx.NOT() != null) return Value.FromBool(!prim.AsBool());
        return prim;
    }

    public override Value VisitPrimary(RedLang.PrimaryContext ctx)
    {
        if (ctx.literal() != null)
            return Visit(ctx.literal());

        if (ctx.expression() != null)
            return Visit(ctx.expression());

        if (ctx.functionCall() != null)
            return Visit(ctx.functionCall());

        if (ctx.IDENTIFIER() != null)
        {
            var name = ctx.IDENTIFIER().GetText();
            var v = ResolveVar(name);
            if (v == null)
                throw new Exception($"La variable '{name}' no está declarada.");
            return v.Val;
        }

        return Value.Null();
    }


    public override Value VisitLiteral(RedLang.LiteralContext ctx)
    {
        if (ctx.INT_LIT() != null) return Value.FromInt(long.Parse(ctx.INT_LIT().GetText(), CultureInfo.InvariantCulture));
        if (ctx.FLOAT_LIT() != null) return Value.FromFloat(double.Parse(ctx.FLOAT_LIT().GetText(), CultureInfo.InvariantCulture));
        if (ctx.TRUE() != null) return Value.FromBool(true);
        if (ctx.FALSE() != null) return Value.FromBool(false);
        if (ctx.STRING_LIT() != null)
        {
            var raw = ctx.STRING_LIT().GetText();
            var inner = raw.Length >= 2 ? raw.Substring(1, raw.Length - 2) : "";
            return Value.FromString(Unescape(inner));
        }
        return Value.Null();
    }

    public override Value VisitType(RedLang.TypeContext ctx)
        => Value.FromString(ctx.GetText());
}