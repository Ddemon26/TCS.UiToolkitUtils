using System;

namespace TCS.UiToolkitUtils.Attributes {
    [AttributeUsage( AttributeTargets.Field )]
    public sealed class USSNameAttribute : Attribute {
        string Alias { get; }
    
        public USSNameAttribute() { }
    
        public USSNameAttribute( string alias ) {
            Alias = alias;
        }
    }
}