using System;
using System.Collections.Generic;
using System.Text;

namespace DeepEqual.Generator
{
    public sealed class CodeWriter
    {
        private readonly StringBuilder _buffer = new();

        public override string ToString() => _buffer.ToString();

        // ---- low-level ----
        public void Write(string text) => _buffer.Append(text);

        public void WriteLine(string text = "")
        {
            if (text.Length > 0)
                _buffer.AppendLine(text);
        }

        public void Line(string text = "") => WriteLine(text);

        public void BlankLine() => _buffer.AppendLine();

        // ---- classic Open/Close ----
        public void Open(string header)
        {
            if (!string.IsNullOrEmpty(header))
                WriteLine(header);
            WriteLine("{");
        }

        public void Close()
        {
            WriteLine("}");
        }

        // ---- lambda/RAII helpers ----
        public void Open(string header, Action body)
        {
            if (body is null) throw new ArgumentNullException(nameof(body));
            Open(header);
            body();
            Close();
        }

        // ---- structured helpers ----

        public void Method(string signature, Action body)
            => Open(signature, body);

        public void ForRaw(string clause, Action body)
            => Open($"for ({clause})", body);

        public void Foreach(string declaration, string enumerable, Action body)
            => Open($"foreach ({declaration} in {enumerable})", body);

        public void Foreach(string type, string name, string enumerable, Action body)
            => Open($"foreach ({type} {name} in {enumerable})", body);

        // New: while
        public void While(string condition, Action body)
            => Open($"while ({condition})", body);

        // New: do { ... } while (cond);
        public void DoWhile(string condition, Action body)
        {
            if (body is null) throw new ArgumentNullException(nameof(body));
            WriteLine("do");
            WriteLine("{");
            body();
            WriteLine("} while (" + condition + ");");
        }

        // New: using (...) { ... }
        public void Using(string resource, Action body)
            => Open($"using ({resource})", body);

        // New: lock (...)
        public void Lock(string expr, Action body)
            => Open($"lock ({expr})", body);

        // New: checked/unchecked
        public void Checked(Action body)
            => Open("checked", body);

        public void Unchecked(Action body)
            => Open("unchecked", body);

        // Optional niceties for declarations
        public void Namespace(string name, Action body)
            => Open($"namespace {name}", body);

        public void Class(string signature, Action body)
            => Open(signature, body);

        public void Struct(string signature, Action body)
            => Open(signature, body);

        public void Interface(string signature, Action body)
            => Open(signature, body);
    }

    // ---------------- if / else-if / else (original pattern preserved) ----------------
    public record IfBlock(CodeWriter Writer);
    public static class IfChain
    {
        public static IfBlock If(this CodeWriter writer, string condition, Action thenBody)
        {
            writer.Open($"if ({condition})", thenBody);
            return new IfBlock(writer);
        }

        public static IfBlock ElseIf(this IfBlock ifBlock, string condition, Action thenBody)
        {
            ifBlock.Writer.Open($"else if ({condition})", thenBody);
            return ifBlock;
        }

        // NOTE: signature preserved exactly as provided (unused 'condition')
        public static void Else(this IfBlock ifBlock, Action thenBody)
            => ifBlock.Writer.Open("else", thenBody);
    }

    // ---------------- try / catch / finally ----------------
    public record TryBlock(CodeWriter Writer);
    public static class TryChain
    {
        public static TryBlock Try(this CodeWriter writer, Action tryBody)
        {
            writer.Open("try", tryBody);
            return new TryBlock(writer);
        }

        // catch (Exception ex)
        public static TryBlock Catch(this TryBlock blk, string exceptionDeclaration, Action catchBody)
        {
            blk.Writer.Open($"catch ({exceptionDeclaration})", catchBody);
            return blk;
        }

        // catch when (...)
        public static TryBlock CatchWhen(this TryBlock blk, string exceptionDeclaration, string whenCondition, Action catchBody)
        {
            blk.Writer.Open($"catch ({exceptionDeclaration}) when ({whenCondition})", catchBody);
            return blk;
        }

        public static void Finally(this TryBlock blk, Action finallyBody)
            => blk.Writer.Open("finally", finallyBody);
    }

    // ---------------- switch / case / default ----------------
    public readonly record struct SwitchBlock(CodeWriter Writer);
    public static class SwitchChain
    {
        // Usage:
        // writer.Switch("expr", sw => {
        //   sw.Case("1", () => { ... });
        //   sw.Case("\"foo\"", () => { ... });
        //   sw.Default(() => { ... });
        // });
        public static void Switch(this CodeWriter writer, string expression, Action<SwitchBlock> body)
        {
            if (body is null) throw new ArgumentNullException(nameof(body));
            writer.Open($"switch ({expression})");
            body(new SwitchBlock(writer));
            writer.Close();
        }

        // Writes:
        // case <label>:
        // {
        //    ...
        //    break;
        // }
        public static SwitchBlock Case(this SwitchBlock sw, string label, Action body)
        {
            sw.Writer.WriteLine($"case {label}:");
            sw.Writer.WriteLine("{");
            body?.Invoke();
            sw.Writer.WriteLine("break;");
            sw.Writer.WriteLine("}");
            return sw;
        }

        public static void Default(this SwitchBlock sw, Action body)
        {
            sw.Writer.WriteLine("default:");
            sw.Writer.WriteLine("{");
            body?.Invoke();
            sw.Writer.WriteLine("break;");
            sw.Writer.WriteLine("}");
        }
    }
}
