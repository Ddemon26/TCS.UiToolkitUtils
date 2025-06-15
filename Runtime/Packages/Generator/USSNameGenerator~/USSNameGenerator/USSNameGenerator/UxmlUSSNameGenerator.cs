using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TCS.Generators;

[Generator( LanguageNames.CSharp )]
public sealed class UxmlUssNameGenerator : ISourceGenerator {
    const string ATTRIBUTE_NAME = "USSNameAttribute";
    const string ATTRIBUTE_NAMESPACE = "TCS.UiToolkitUtils.Attributes";
    const string FULLY_QUALIFIED_ATTRIBUTE_NAME = ATTRIBUTE_NAMESPACE + "." + ATTRIBUTE_NAME;
    const string USS_NAME = "USSName";
    const string SET_CLASS_LIST_METHOD = "SetElementClassNames";
    const string VISUAL_ELEMENT_FQN = "UnityEngine.UIElements.VisualElement";

    public void Initialize(GeneratorInitializationContext ctx) =>
        ctx.RegisterForSyntaxNotifications( () => new SyntaxReceiver() );

    public void Execute(GeneratorExecutionContext ctx) {
        if ( ctx.SyntaxReceiver is not SyntaxReceiver rx ) return;

        // symbols we need
        var ussAttr = ctx.Compilation.GetTypeByMetadataName( FULLY_QUALIFIED_ATTRIBUTE_NAME );
        var visualElement = ctx.Compilation.GetTypeByMetadataName( VISUAL_ELEMENT_FQN );
        if ( ussAttr is null || visualElement is null ) return; // can’t validate

        Dictionary<INamedTypeSymbol, List<IFieldSymbol>> classes = new(SymbolEqualityComparer.Default);

        // ----------------------- gather fields ------------------------------
        foreach (var fieldDecl in rx.Candidates) {
            var model = ctx.Compilation.GetSemanticModel( fieldDecl.SyntaxTree );
            foreach (var varDecl in fieldDecl.Declaration.Variables) {
                if ( model.GetDeclaredSymbol( varDecl ) is not IFieldSymbol field ) continue;

                // must have [USSName] …
                if ( !field.GetAttributes().Any( a => SymbolEqualityComparer.Default.Equals( a.AttributeClass, ussAttr ) ) )
                    continue;

                // …and the field’s type must inherit from VisualElement
                if ( !DerivesFrom( field.Type, visualElement ) )
                    continue;

                if ( !classes.TryGetValue( field.ContainingType, out List<IFieldSymbol> list ) )
                    classes[field.ContainingType] = list = [];
                list.Add( field );
            }
        }

        // ------------------- generate one partial per class -----------------
        foreach (KeyValuePair<INamedTypeSymbol, List<IFieldSymbol>> kv in classes) // no deconstruction
        {
            var cls = kv.Key;
            List<IFieldSymbol> fields = kv.Value;

            bool inNamespace = !cls.ContainingNamespace.IsGlobalNamespace;
            string nsOpen = inNamespace ? $"namespace {cls.ContainingNamespace}\n{{\n" : "";
            string nsClose = inNamespace ? "}\n" : "";

            string blockName = ToKebab( cls.Name ); // BEM “block”

            var sb = new StringBuilder();
            sb.Append( nsOpen );
            sb.Append( $"    public partial class {cls.Name}\n    {{\n" );
            sb.Append( $"        public static readonly string ClassNameUSS = \"{blockName}\";\n\n" );

            // ---- 1) constants ----------------------------------------------
            foreach (var f in fields) {
                string raw = f.Name.StartsWith( "m_" ) ? f.Name.Substring( 2 ) : f.Name.TrimStart( '_' );
                string suffix = ToBemSuffix( raw ); // element‑modifier
                string idName = char.ToUpper( raw[0] ) + raw.Substring( 1 ) + "USS";

                sb.Append( $"        public static readonly string {idName} = ClassNameUSS + \"_{suffix}\"; // {blockName}_{suffix} \n" );
            }

            // ---- 2) helper to add classes ----------------------------------
            sb.Append( $"\n        public void {SET_CLASS_LIST_METHOD}()\n        {{\n" );
            sb.Append( "            AddToClassList(ClassNameUSS);\n" );
            foreach (var f in fields) {
                string raw = f.Name.StartsWith( "m_" ) ? f.Name.Substring( 2 ) : f.Name.TrimStart( '_' );
                string idName = char.ToUpper( raw[0] ) + raw.Substring( 1 ) + "USS";
                sb.Append( $"            {f.Name}.AddToClassList({idName});\n" );
            }

            sb.Append( "        }\n    }\n" );
            sb.Append( nsClose );

            ctx.AddSource( $"{cls.Name}_USS.g.cs", SourceText.From( sb.ToString(), Encoding.UTF8 ) );
        }
    }

    // ----------------------------------------------------------------------
    static bool DerivesFrom(ITypeSymbol type, ITypeSymbol baseType) {
        for (var t = type; t is not null; t = t.BaseType)
            if ( SymbolEqualityComparer.Default.Equals( t, baseType ) )
                return true;
        return false;
    }

    static string ToKebab(string s) => string.Join( "-", SplitPascal( s ) );

    static List<string> SplitPascal(string s) =>
        Regex.Matches( s, @"[A-Z]+(?![a-z])|[A-Z]?[a-z]+|\d+" )
            .Cast<Match>()
            .Select( m => m.Value.ToLowerInvariant() )
            .ToList();

    static string ToBemSuffix(string raw) {
        List<string> toks = SplitPascal( raw );
        if ( toks.Count == 1 ) return toks[0]; // element only

        string element = string.Join( "-", toks.Take( toks.Count - 1 ) );
        string modifier = toks[toks.Count - 1];
        return $"{element}-{modifier}";
    }

    // ----------------------------------------------------------------------
    private sealed class SyntaxReceiver : ISyntaxReceiver {
        public List<FieldDeclarationSyntax> Candidates { get; } = [];

        public void OnVisitSyntaxNode(SyntaxNode node) {
            if ( node is FieldDeclarationSyntax f &&
                 f.AttributeLists.Count > 0 &&
                 f.AttributeLists.SelectMany( al => al.Attributes )
                     .Any( a => a.Name.ToString() == USS_NAME || a.Name.ToString() == ATTRIBUTE_NAME || a.Name.ToString() == FULLY_QUALIFIED_ATTRIBUTE_NAME ) ) {
                Candidates.Add( f );
            }
        }
    }
}