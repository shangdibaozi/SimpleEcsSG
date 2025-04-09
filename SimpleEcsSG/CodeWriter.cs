using System;
using System.Text;

public sealed class CodeWriter
{
    private readonly StringBuilder _buffer = new StringBuilder();

    private int _indentLevel;

    public void AppendLine(string value = "")
    {
        if (string.IsNullOrEmpty(value))
        {
            _buffer.AppendLine();
        }
        else
        {
            _buffer.AppendLine($"{new string(' ', _indentLevel * 4)}{value}");
        }
    }

    public void IncreaseIndent()
    {
        _indentLevel++;
    }

    public void DecreaseIndent()
    {
        if (_indentLevel > 0)
        {
            _indentLevel--;
        }
    }

    public void BeginBlock()
    {
        AppendLine("{");
        IncreaseIndent();
    }

    public void EndBlock(bool withSemicolon = false)
    {
        DecreaseIndent();
        AppendLine(withSemicolon ? "};" : "}");
    }

    public void Clear()
    {
        _buffer.Clear();
        _indentLevel = 0;
    }

    public override string ToString()
    {
        return _buffer.ToString();
    }

    public IDisposable BeginIndentScope() => new IndentScope(this);

    public IDisposable BeginBlockScope(string startLine = null, bool withSemicolon = false) => new BlockScope(this, startLine, withSemicolon);

    private readonly struct IndentScope : IDisposable
    {
        private readonly CodeWriter _codeWriter;

        public IndentScope(CodeWriter codeWriter)
        {
            _codeWriter = codeWriter;
            _codeWriter.IncreaseIndent();
        }

        public void Dispose()
        {
            _codeWriter.DecreaseIndent();
        }
    }
    
    private readonly struct BlockScope : IDisposable
    {
        private readonly CodeWriter _codeWriter;
        private readonly bool _withSemicolon;

        public BlockScope(CodeWriter codeWriter, string startLine = null, bool withSemicolon = false)
        {
            _codeWriter = codeWriter;
            _withSemicolon = withSemicolon;
            _codeWriter.AppendLine(startLine);
            _codeWriter.BeginBlock();
        }

        public void Dispose()
        {
            _codeWriter.EndBlock(_withSemicolon);
        }
    }
}