using System;
using System.Collections.Generic;
using System.Text;

namespace DeepEqual.Generator
{
    internal sealed class CodeWriter
    {
        private readonly StringBuilder _buffer = new();
        private int _indent;
        private readonly string _indentUnit;

        public CodeWriter(string indentUnit = "    ")
        {
            _indentUnit = indentUnit ?? "    ";
        }

        public override string ToString() => _buffer.ToString();

        // ---- low-level ----
        public void Write(string text) => _buffer.Append(text);

        public void WriteLine(string text = "")
        {
            if (text.Length > 0)
                _buffer.Append(IndentString());
            _buffer.AppendLine(text);
        }

        public void Line(string text = "") => WriteLine(text);

        public void BlankLine() => _buffer.AppendLine();

        private string IndentString()
        {
            if (_indent == 0) return string.Empty;
            return string.Concat(System.Linq.Enumerable.Repeat(_indentUnit, _indent));
        }

        // ---- classic Open/Close ----
        public void Open(string header)
        {
            if (!string.IsNullOrEmpty(header))
                WriteLine(header);
            WriteLine("{");
            _indent++;
        }

        public void Close()
        {
            _indent = Math.Max(0, _indent - 1);
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

        public IDisposable Block(string header)
        {
            Open(header);
            return new Scope(this);
        }

        /// <summary>Headerless braces: writes only '{ ... }' at the current indentation.</summary>
        public void Braces(Action body)
        {
            if (body is null) throw new ArgumentNullException(nameof(body));
            WriteLine("{");
            _indent++;
            try { body(); }
            finally { Close(); }
        }

        public void Indent(Action body)
        {
            if (body is null) throw new ArgumentNullException(nameof(body));
            _indent++;
            try { body(); }
            finally { _indent = Math.Max(0, _indent - 1); }
        }

        private sealed class Scope : IDisposable
        {
            private CodeWriter? _w;
            public Scope(CodeWriter w) { _w = w; }
            public void Dispose()
            {
                var w = System.Threading.Interlocked.Exchange(ref _w, null);
                if (w != null) w.Close();
            }
        }

        // ---- structured helpers ----

        public void Method(string signature, Action body)
            => Open(signature, body);

        public void If(string condition, Action thenBody)
            => Open($"if ({condition})", thenBody);

        public void If(string condition, Action thenBody, Action? elseBody)
        {
            If(condition, thenBody);
            if (elseBody != null)
            {
                Line("else");
                Braces(elseBody);
            }
        }

        public IfChainBuilder IfChain(string condition, Action body)
        {
            Line($"if ({condition})");
            WriteLine("{");
            _indent++;
            body();
            _indent--;
            WriteLine("}");
            return new IfChainBuilder(this);
        }

        public sealed class IfChainBuilder
        {
            private readonly CodeWriter _w;
            internal IfChainBuilder(CodeWriter w) { _w = w; }
            public IfChainBuilder ElseIf(string condition, Action body)
            {
                _w.Line($"else if ({condition})");
                _w.WriteLine("{");
                _w._indent++;
                body();
                _w._indent--;
                _w.WriteLine("}");
                return this;
            }
            public void Else(Action body)
            {
                _w.Line("else");
                _w.Braces(body);
            }
        }

        public void Try(Action tryBody,
                       IEnumerable<(string CatchHeader, Action Body)>? catches = null,
                       Action? finallyBody = null)
        {
            Open("try", tryBody);
            if (catches != null)
            {
                foreach (var (header, body) in catches)
                {
                    Open(header, body);
                }
            }
            if (finallyBody != null)
            {
                Open("finally", finallyBody);
            }
        }

        public void For(string init, string condition, string increment, Action body)
            => Open($"for ({init}; {condition}; {increment})", body);

        /// <summary>Use for already-composed for-clause: e.g. "int i=0; i<n; i++"</summary>
        public void ForRaw(string clause, Action body)
            => Open($"for ({clause})", body);

        public void Foreach(string declaration, string enumerable, Action body)
            => Open($"foreach ({declaration} in {enumerable})", body);

        public void Foreach(string type, string name, string enumerable, Action body)
            => Open($"foreach ({type} {name} in {enumerable})", body);

        public void Switch(string expression, Action<SwitchBuilder> buildCases)
        {
            if (buildCases is null) throw new ArgumentNullException(nameof(buildCases));
            Line($"switch ({expression})");
            WriteLine("{");
            _indent++;
            var builder = new SwitchBuilder(this);
            buildCases(builder);
            _indent--;
            WriteLine("}");
        }

        public sealed class SwitchBuilder
        {
            private readonly CodeWriter _w;
            internal SwitchBuilder(CodeWriter w) { _w = w; }
            public SwitchBuilder Case(string pattern, Action body)
            {
                _w.Line($"case {pattern}:");
                _w.Indent(body);
                return this;
            }
            public void Default(Action body)
            {
                _w.Line("default:");
                _w.Indent(body);
            }
        }
    }
}
