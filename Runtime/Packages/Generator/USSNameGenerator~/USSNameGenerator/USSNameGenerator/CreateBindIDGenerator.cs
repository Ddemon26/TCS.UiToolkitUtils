using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
namespace TCS.Generators;

[Generator] public sealed class CreateBindIdGenerator : ISourceGenerator {
    const string ATTRIBUTE_FULL_NAME = "CreateBindIDAttribute";

    public void Initialize(GeneratorInitializationContext context) {
        // register a syntax receiver that will be called for every syntax node in the compilation
        context.RegisterForSyntaxNotifications( () => new SyntaxReceiver() );
    }

    public void Execute(GeneratorExecutionContext context) {
        // retrieve the populated receiver 
        if ( context.SyntaxReceiver is not SyntaxReceiver receiver )
            return;

        // get the attribute symbol so we can compare against it
        var attrSymbol = context.Compilation.GetTypeByMetadataName( ATTRIBUTE_FULL_NAME );
        if ( attrSymbol == null )
            return;

        // group annotated members by their containing type
        Dictionary<INamedTypeSymbol, List<IPropertySymbol>> membersByType = new(SymbolEqualityComparer.Default);

        foreach (var propDecl in receiver.CandidateProperties) {
            var model = context.Compilation.GetSemanticModel( propDecl.SyntaxTree );
            if ( model.GetDeclaredSymbol( propDecl ) is not IPropertySymbol propSym )
                continue;

            // check that it really has [CreateBindID]
            if ( !propSym.GetAttributes().Any( a => SymbolEqualityComparer.Default.Equals( a.AttributeClass, attrSymbol ) ) )
                continue;

            var container = propSym.ContainingType;
            if ( !membersByType.TryGetValue( container, out List<IPropertySymbol> list ) )
                membersByType[container] = list = new List<IPropertySymbol>();
            list.Add( propSym );
        }

        // for each type, emit a partial class with the BindingId fields
        foreach (KeyValuePair<INamedTypeSymbol, List<IPropertySymbol>> kv in membersByType) {
            var cls = kv.Key;
            List<IPropertySymbol> members = kv.Value;
            string ns = cls.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : $"namespace {cls.ContainingNamespace.ToDisplayString()}\n{{\n";

            var sb = new StringBuilder();
            sb.AppendLine( "using Unity.Properties;" );
            sb.AppendLine( "using UnityEngine.UIElements;" );
            sb.AppendLine();
            sb.Append( ns );
            sb.AppendLine( $"    public partial class {cls.Name}" );
            sb.AppendLine( "    {" );

            foreach (var m in members) {
                string propName = m.Name;
                string fieldName = propName + "Property";
                sb.AppendLine( $"        public static readonly BindingId {fieldName} = (BindingId)nameof({propName});" );
            }

            sb.AppendLine( "    }" );

            if ( !string.IsNullOrEmpty( ns ) )
                sb.AppendLine( "}" );

            // add to compilation
            context.AddSource
            (
                $"{cls.Name}_BindingId.g.cs",
                SourceText.From( sb.ToString(), Encoding.UTF8 )
            );
        }
    }

    /// <summary>
    /// collects all property declarations that have at least one attribute and whose name syntax ends with "CreateBindID"
    /// </summary>
    class SyntaxReceiver : ISyntaxReceiver {
        public List<PropertyDeclarationSyntax> CandidateProperties { get; } = new();

        public void OnVisitSyntaxNode(SyntaxNode node) {
            if ( node is not PropertyDeclarationSyntax property ) return;

            IEnumerable<AttributeSyntax> attributes = property.AttributeLists.SelectMany( list => list.Attributes );
            if ( attributes.Any
                ( attribute => attribute.Name
                      .ToString()
                      .EndsWith( "CreateBindID", StringComparison.Ordinal )
                ) ) {
                CandidateProperties.Add( property );
            }
        }
    }
}