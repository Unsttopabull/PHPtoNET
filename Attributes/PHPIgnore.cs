using System;

namespace Frost.PHPtoNET.Attributes {

    /// <summary>Used on fields and properties that should not be serialized.</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class PHPIgnore : Attribute {
    }

}