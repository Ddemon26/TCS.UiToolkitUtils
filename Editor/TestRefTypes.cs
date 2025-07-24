/*#nullable enable
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Serialization;

namespace System.Runtime.CompilerServices {
    [AttributeUsage( AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = false )]
    internal sealed class RequiredMemberAttribute : Attribute { }

    [AttributeUsage( AttributeTargets.All, AllowMultiple = true, Inherited = false )]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute {
        public CompilerFeatureRequiredAttribute(string featureName) { }
    }

    [AttributeUsage( AttributeTargets.Parameter, Inherited = false )]
    internal sealed class CallerArgumentExpressionAttribute : Attribute {
        public CallerArgumentExpressionAttribute(string parameterName) {
            ParameterName = parameterName;
        }

        public string ParameterName { get; }
    }

    public class Unsafe {
        public static bool IsNullRef<T>(ref T value) where T : class {
            return value == null;
        }

        public static bool IsNullRef(ref int value) {
            return value == 0;
        }
    }
}

namespace System.Diagnostics.CodeAnalysis {
    [AttributeUsage( AttributeTargets.Constructor, AllowMultiple = false, Inherited = false )]
    internal sealed class SetsRequiredMembersAttribute : Attribute { }
}

public static class Validation {
    public static void Check(bool condition, [System.Runtime.CompilerServices.CallerArgumentExpression( "condition" )] string? message = null) {
        if ( !condition ) {
            throw new InvalidOperationException( $"Validation failed for: {message}" );
        }
    }
}

public readonly struct RefHolder<T> {
    public RefHolder(T value) => Value = value;
    public T Value { get; }
}

[Serializable] public struct RefFieldExample {
    public required int m_number;
    public void SetNumber(int value) => m_number = value;
    public int GetNumber() => m_number;
}

[Serializable] public readonly record struct RefRecordExample(int Id, string Name);

public class TestRefTypes : MonoBehaviour {
    [SerializeField] RefFieldExample m_refFieldExample;
    [SerializeField] RefRecordExample m_refRecordExample;

    void Start() {
        m_refFieldExample.SetNumber( 42 );
        Debug.Log( $"RefFieldExample Number: {m_refFieldExample.GetNumber()}" );

        m_refRecordExample = new RefRecordExample( 1, "Test" );
        Debug.Log( $"RefRecordExample Id: {m_refRecordExample.Id}, Name: {m_refRecordExample.Name}" );

        const int x = 1;
        const int y = 2;
        try {
            Validation.Check( x > y );
        }
        catch (Exception e) {
            Debug.LogException( e );
        }
    }
}

public class SpanExample : MonoBehaviour
{
    void Start()
    {
        // stackalloc scratch buffer – zero GC
        Span<int> tmp = stackalloc int[4];
        tmp[0] = 42;

        // Wrap a single int in a Span (replacement for RefHolder<T>)
        var number = 7;
        Span<int> one = MemoryMarshal.CreateSpan(ref number, 1);
        one[0] *= 3;                       // number == 21

        Debug.Log($"tmp[0]={tmp[0]}, number={number}");
    }
}*/