using System;
using System.Text;

namespace DeepEqual.Generator;

internal sealed class CodeWriter
{
    private readonly StringBuilder _buffer = new();
    private int _indent;

    public void Line(string text = "")
    {
        if (!string.IsNullOrEmpty(text))
        {
            _buffer.Append(' ', _indent * 4);
            _buffer.AppendLine(text);
        }
        else
        {
            _buffer.AppendLine();
        }
    }

    public void Open(string header)
    {
        Line(header);
        Line("{");
        _indent++;
    }

    public void Close()
    {
        _indent = Math.Max(0, _indent - 1);
        Line("}");
    }

    public override string ToString()
    {
        return _buffer.ToString();
    }
}