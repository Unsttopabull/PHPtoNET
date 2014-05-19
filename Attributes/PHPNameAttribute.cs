using System;

namespace Frost.PHPtoNET.Attributes {

    /// <summary>Represents an attibute to be used when model property/field name is different to the serialized one</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Struct, AllowMultiple = false)]
    public class PHPNameAttribute : Attribute {

        /// <summary>Initializes a new instance of the <see cref="PHPNameAttribute"/> class.</summary>
        /// <param name="phpName">The serialized property/field name.</param>
        public PHPNameAttribute(string phpName) {
            PHPName = phpName;
        }

        /// <summary>Gets the serialized property/field name..</summary>
        /// <value>The serialized property/field name.</value>
        public string PHPName { get; private set; }
    }
}
