using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System;
using System.Collections.Generic;
using System.Diagnostics;

public ref struct SpanStack<T>
{
    private Span<T> memory;
    private int index;
    private int size;

    public SpanStack(Span<T> mem) { memory = mem; index = 0; size = mem.Length; }

    public bool IsEmpty() => index < 0;
    public bool IsFull() => index > size - 1;

    public void Push(T item) => memory[index++] = item;
    public T Pop() => memory[--index];
}

public static class SpanExtensions
{
    public static SpanStack<T> AsStack<T>(this Span<T> span) => new SpanStack<T>(span);
}

abstract class Token {}

sealed class Operand: Token
{
    public int Value { get; }
    public Operand(int v) { Value = v; }
}

abstract class Operator: Token {
    abstract public int Calc(int a, int b);
}

sealed class Add: Operator
{
    public override int Calc(int a, int b) => a + b;
}

sealed class Mult : Operator
{
    public override int Calc(int a, int b) => a * b;
}

sealed class Minus : Operator
{
    public override int Calc(int a, int b) => a - b;
}

public enum TokenType { Operand, Sum, Mult, Minus}

readonly struct SToken
{
    public TokenType Type { get; }
    public int Value { get; }
    public SToken(TokenType t, int v) { Type = t; Value = v; }
    public SToken(TokenType t) { Type = t; Value = 0; }

    public int Calc(int a, int b) => 
               Type == TokenType.Sum   ? a + b :
               Type == TokenType.Minus ? a - b :
               Type == TokenType.Minus ? a * b :
               throw new Exception("I don't know that one");

}

public class Program
{
    static Token[] tokens;
    static SToken[] stokens;

    [GlobalSetup]
    public void Setup()
    {
        tokens = new Token[] { new Operand(2), new Operand(3), new Operand(4), new Add(),
                               new Mult(), new Operand(5), new Minus() };
        stokens = new SToken[] { new SToken(TokenType.Operand, 2),
                                 new SToken(TokenType.Operand, 3), new SToken(TokenType.Operand, 4),
                                 new SToken(TokenType.Sum),  new SToken(TokenType.Mult),
                                 new SToken(TokenType.Operand, 5), new SToken(TokenType.Minus)};
    }

    [Benchmark(Baseline = true)]
    public int PostfixEvalSpanStackStructTypes()
    {
        Span<SToken> span = stackalloc SToken[100];
        var stack = span.AsStack();

        foreach (var token in stokens)
        {
            if (token.Type == TokenType.Operand)
            {
                stack.Push(token);
            } else {
                var a = stack.Pop();
                var b = stack.Pop();
                var result = token.Calc(a.Value, b.Value);
                stack.Push(new SToken(TokenType.Operand, result));
                break;
            }
        }
        return stack.Pop().Value;
    }

    [Benchmark]
    public int PostfixEvalSpanStack()
    {
        Span<Token> span = new Token[100];
        var stack = span.AsStack();

        foreach (var token in tokens)
        {
            switch (token)
            {
                case Operand t:
                    stack.Push(t);
                    break;
                case Operator o:
                    var a = stack.Pop() as Operand;
                    var b = stack.Pop() as Operand;
                    var result = o.Calc(a.Value, b.Value);
                    stack.Push(new Operand(result));
                    break;
            }
        }
        return (stack.Pop() as Operand).Value;
    }

    [Benchmark]
    public int PostfixEvalStack()
    {
        var stack = new Stack<Token>(100);

        foreach (var token in tokens)
        {
            switch (token)
            {
                case Operand t:
                    stack.Push(t);
                    break;
                case Operator o:
                    var a = stack.Pop() as Operand;
                    var b = stack.Pop() as Operand;
                    var result = o.Calc(a.Value, b.Value);
                    stack.Push(new Operand(result));
                    break;
            }
        }
        return (stack.Pop() as Operand).Value;
    }

    static void Test()
    {
        var p = new Program();
        p.Setup();
        Trace.Assert(p.PostfixEvalStack() == p.PostfixEvalSpanStack() &&
                     p.PostfixEvalSpanStack() == p.PostfixEvalSpanStackStructTypes());
    }
    static void Main(string[] args)
    {
        Test();
        var summary = BenchmarkRunner.Run<Program>();
    }
}
