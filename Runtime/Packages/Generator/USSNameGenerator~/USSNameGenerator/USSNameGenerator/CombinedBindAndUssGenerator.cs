/*using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TCS.Generators;

[Generator]
public class CombinedBindAndUssGenerator : ISourceGenerator {
    const string BindAttr = "TCS_CustomElements.CreateBindIDAttribute";
    const string UssAttr = "TCS_CustomElements.USSNameAttribute";
    const string SetMethodName = "SetElementClassNames";

    public void Initialize(GeneratorInitializationContext ctx) {
        ctx.RegisterForSyntaxNotifications( () => new Receiver() );
    }

    public void Execute(GeneratorExecutionContext ctx) {
        if ( ctx.SyntaxReceiver is not Receiver r ) return;

        var bindSym = ctx.Compilation.GetTypeByMetadataName( BindAttr );
        var ussSym = ctx.Compilation.GetTypeByMetadataName( UssAttr );
        if ( bindSym == null && ussSym == null ) return;

        // key = class, value = (list of [CreateBindID] props, list of [USSName] fields)
        var map = new Dictionary<INamedTypeSymbol, (List<IPropertySymbol> props, List<IFieldSymbol> fields)>
        (
            SymbolEqualityComparer.Default
        );

        // 1) collect properties
        foreach (var prop in r.PropDecls) {
            var model = ctx.Compilation.GetSemanticModel( prop.SyntaxTree );
            if ( model.GetDeclaredSymbol( prop ) is not IPropertySymbol ps ) continue;
            if ( bindSym != null && ps.GetAttributes().Any( a => SymbolEqualityComparer.Default.Equals( a.AttributeClass, bindSym ) ) ) {
                var cls = ps.ContainingType;
                if ( !map.TryGetValue( cls, out var tup ) )
                    map[cls] = tup = (new(), new());
                tup.props.Add( ps );
            }
        }

        // 2) collect fields
        foreach (var fldDecl in r.FieldDecls) {
            var model = ctx.Compilation.GetSemanticModel( fldDecl.SyntaxTree );
            foreach (var v in fldDecl.Declaration.Variables) {
                if ( model.GetDeclaredSymbol( v ) is not IFieldSymbol fs ) continue;
                if ( ussSym != null && fs.GetAttributes().Any( a => SymbolEqualityComparer.Default.Equals( a.AttributeClass, ussSym ) ) ) {
                    var cls = fs.ContainingType;
                    if ( !map.TryGetValue( cls, out var tup ) )
                        map[cls] = tup = (new(), new());
                    tup.fields.Add( fs );
                }
            }
        }

        // emit one file per class
        foreach (var kv in map) {
            var cls = kv.Key;
            var ps = kv.Value.props;
            var fs = kv.Value.fields;
            bool hasNs = !cls.ContainingNamespace.IsGlobalNamespace;
            var nsOpen = hasNs ? $"namespace {cls.ContainingNamespace.ToDisplayString()}\n{{\n" : "";
            var nsClose = hasNs ? "}\n" : "";

            var sb = new StringBuilder();
            sb.AppendLine( "using Unity.Properties;" );
            sb.AppendLine( "using UnityEngine.UIElements;" ); // if you need it
            sb.AppendLine();
            sb.Append( nsOpen );
            sb.AppendLine( $"    public partial class {cls.Name}" );
            sb.AppendLine( "    {" );

            // A) BindingId properties
            foreach (var p in ps) {
                var name = p.Name;
                var fieldId = name + "Property";
                sb.AppendLine( $"        public static readonly BindingId {fieldId} = (BindingId)nameof({name});" );
            }

            if ( ps.Count > 0 && fs.Count > 0 )
                sb.AppendLine();

            // B) USSName constants + SetElementClassNames
            if ( fs.Count > 0 ) {
                // kebab‐case helper
                string ToKebab(string s) {
                    var t = new StringBuilder();
                    for (int i = 0; i < s.Length; i++) {
                        if ( char.IsUpper( s[i] ) && i > 0 ) t.Append( '-' );
                        t.Append( char.ToLowerInvariant( s[i] ) );
                    }

                    return t.ToString();
                }

                var baseName = ToKebab( cls.Name );
                sb.AppendLine( $"        public static readonly string ClassNameUSS = \"{baseName}\";" );
                sb.AppendLine();
                foreach (var f in fs) {
                    var raw = f.Name.StartsWith( "m_" ) ? f.Name.Substring( 2 ) : f.Name.TrimStart( '_' );
                    var suf = ToKebab( raw );
                    var idName = char.ToUpperInvariant( raw[0] ) + raw.Substring( 1 ) + "USS";
                    sb.AppendLine( $"        public static readonly string {idName} = ClassNameUSS + \"_{suf}\";" );
                }

                sb.AppendLine();
                sb.AppendLine( $"        public void {SetMethodName}()" );
                sb.AppendLine( "        {" );
                foreach (var f in fs) {
                    var raw = f.Name.StartsWith( "m_" ) ? f.Name.Substring( 2 ) : f.Name.TrimStart( '_' );
                    var idName = char.ToUpperInvariant( raw[0] ) + raw.Substring( 1 ) + "USS";
                    sb.AppendLine( $"            {f.Name}.AddToClassList({idName});" );
                }

                sb.AppendLine( "        }" );
            }

            sb.AppendLine( "    }" );
            sb.Append( nsClose );

            ctx.AddSource
            (
                $"{cls.Name}.g.cs",
                SourceText.From( sb.ToString(), Encoding.UTF8 )
            );
        }
    }

    class Receiver : ISyntaxReceiver {
        public List<PropertyDeclarationSyntax> PropDecls { get; } = new();
        public List<FieldDeclarationSyntax> FieldDecls { get; } = new();

        public void OnVisitSyntaxNode(SyntaxNode node) {
            if ( node is PropertyDeclarationSyntax p && p.AttributeLists.Count > 0 )
                PropDecls.Add( p );

            if ( node is FieldDeclarationSyntax f && f.AttributeLists.Count > 0 )
                FieldDecls.Add( f );
        }
    }
}*/