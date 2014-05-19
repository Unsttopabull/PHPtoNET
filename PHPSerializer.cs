using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;
using Frost.PHPtoNET.Attributes;

namespace Frost.PHPtoNET {

    /// <summary></summary>
    public class PHPSerializer {
        private bool _private;
        private bool _static;
        private const BindingFlags PUBLIC_FLAGS = BindingFlags.GetField | BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
        private BindingFlags _usedFlags;
        private static readonly Type PHPNameType = typeof(PHPNameAttribute);

        /// <summary>Initializes a new instance of the <see cref="PHPSerializer"/> class.</summary>
        public PHPSerializer() {
            _usedFlags = PUBLIC_FLAGS;
        }

        /// <summary>Gets or sets a value indicating whether private (private/internal/protected) members are to be serialized aswell.</summary>
        /// <value>Is <c>true</c> if private (private/internal/protected) members are to be serialized; otherwise, <c>false</c></value>
        public bool SerializePrivateMembers {
            get { return _private; }
            set {
                if (value) {
                    _private = true;
                    _usedFlags |= BindingFlags.NonPublic;
                }
                else {
                    _private = false;
                    _usedFlags &= ~BindingFlags.NonPublic;
                }
            }
        }

        /// <summary>Gets or sets a value indicating whether static members are to be serialized aswell.</summary>
        /// <value>Is <c>true</c> if static members are to be serialized; otherwise, <c>false</c></value>
        public bool SerializeStaticMembers {
            get { return _static; }
            set {
                if (value) {
                    _static = true;
                    _usedFlags |= BindingFlags.Static;
                }
                else {
                    _static = false;
                    _usedFlags &= ~BindingFlags.Static;
                }
            }
        }

        /// <summary>Serializes the specified object, struct or primitive.</summary>
        /// <param name="obj">The object, struct or primitive to serialize.</param>
        /// <returns></returns>
        public string Serialize(object obj) {
            if (obj == null) {
                return "N;";
            }

            return SerailizeMemberInfo(obj);
        }

        private string SerializeClass<T>(T obj, Type type) {
            MemberInfo[] memberInfos = type.GetMembers(_usedFlags)
                                           .Where(mi => mi.MemberType != MemberTypes.Method &&
                                                        mi.MemberType != MemberTypes.Constructor &&
                                                        mi.MemberType != MemberTypes.NestedType &&
                                                        mi.MemberType != MemberTypes.TypeInfo &&
                                                        !mi.IsDefined(typeof(NonSerializedAttribute), false) &&
                                                        !mi.IsDefined(typeof(XmlIgnoreAttribute), false) &&
                                                        !mi.IsDefined(typeof(PHPIgnore), false) &&
                                                        !mi.Name.EndsWith(">__BackingField") //Auto-property backing field
                                                        )
                                           .ToArray();

            StringBuilder sb = new StringBuilder();

            string typeName = type.Name;
            PHPNameAttribute[] customAttributes = (PHPNameAttribute[]) type.GetCustomAttributes(PHPNameType, false);
            if (customAttributes.Length == 1) {
                typeName = customAttributes[0].PHPName;
            }

            sb.Append(string.Format("O:{0}:\"{1}\":{2}:{{", Encoding.UTF8.GetByteCount(typeName), typeName, memberInfos.Length));

            foreach (MemberInfo member in memberInfos) {
                sb.Append(SerializeMember(obj, member));
            }

            sb.Append("}");
            return sb.ToString();
        }

        private string SerializeMember<T>(T obj, MemberInfo memberInfo) {
            Type memberType = memberInfo.MemberType == MemberTypes.Property ? ((PropertyInfo) memberInfo).PropertyType : ((FieldInfo) memberInfo).FieldType;
            object value = memberInfo.MemberType == MemberTypes.Property ? ((PropertyInfo) memberInfo).GetValue(obj, new object[] { }) : ((FieldInfo) memberInfo).GetValue(obj);

            //bool debug = memberType.Name != "String" && !memberType.IsPrimitive && !memberType.IsValueType && !memberType.IsArray;
            string memberName = memberInfo.IsDefined(PHPNameType, false)
                                    ? ((PHPNameAttribute) memberInfo.GetCustomAttributes(PHPNameType, false)[0]).PHPName
                                    : memberInfo.Name;

            string prefix = string.Format("s:{0}:\"{1}\";", Encoding.UTF8.GetByteCount(memberName), memberName);

            string memberInfoSer = SerailizeMemberInfo(value, memberType);
            if (!string.IsNullOrEmpty(memberInfoSer)) {
                string serializedMember = prefix + memberInfoSer;
                return serializedMember;
            }
            return "";
        }

        private string SerailizeMemberInfo(object value, Type memberType = null) {
            if (memberType == null) {
                memberType = value.GetType();
            }

            if (memberType == typeof(string)) {
                return SerializeString(value as string);
            }

            if (memberType.IsEnum) {
                return SerializeString(value.ToString());
            }

            if (memberType.IsPrimitive) {
                return SerializePrimitive(memberType, value);
            }

            if (memberType.GetInterface("IDictionary") != null) {
                if (memberType.IsGenericType) {
                    Type[] arguments = memberType.GetGenericArguments();

                    return value != null && arguments.Length == 2
                               ? SerializeIDictionary((IDictionary) value, arguments[1])
                               : "N;";                    
                }

                return value != null
                    ? SerializeIDictionary((IDictionary) value)
                    : "N;";
            }

            if (memberType.GetInterface("ICollection") != null) {
                if (memberType.IsGenericType) {
                    Type[] arguments = memberType.GetGenericArguments();

                    return value != null
                               ? SerializeICollection((IEnumerable) value, arguments[0])
                               : "N;";
                }

                return value != null
                           ? SerializeICollection((IEnumerable) value)
                           : "N;";
            }

            if (memberType.IsClass) {
                if (memberType.Module.ScopeName == "CommonLanguageRuntimeLibrary") {
                    return SerializeString(value.ToString());
                }

                return (value == null ? "N;" : SerializeClass(value, memberType));
            }

            //Structs
            if (memberType.IsValueType) {
                if (memberType.Module.ScopeName == "CommonLanguageRuntimeLibrary") {
                    if (memberType.Name == "Nullable`1") {
                        return value != null
                            ? SerailizeMemberInfo(value)
                            : "N;";
                    }

                    return SerializeString(value.ToString());
                }

                return SerializeClass(value, memberType);
            }

            throw new ParsingException(string.Format("Could not serialize member of type \"{0}\"", memberType.FullName));
        }

        private string SerializePrimitive(Type memberType, object value) {
            string postfix = null;
            switch (memberType.Name) {
                case "Boolean":
                    postfix = (bool) value ? "b:1;" : "b:0;";
                    break;
                case "Byte":
                case "SByte":
                case "Int16":
                case "UInt16":
                case "Int32":
                    postfix = string.Format("i:{0};", Convert.ToInt32(value).ToString(CultureInfo.InvariantCulture));
                    break;
                case "UInt32":
                    uint value1 = (uint) value;
                    postfix = string.Format((value1 > int.MaxValue) ? "d:{0};" : "i:{0};", value1.ToString(CultureInfo.InvariantCulture));
                    break;
                case "Char":
                    postfix = SerializeString(((char) value).ToString(CultureInfo.InvariantCulture));
                    break;
                case "Int64":
                case "UInt64":
                case "Single":
                case "Double":
                    postfix = string.Format("d:{0};", Convert.ToDouble(value).ToString(CultureInfo.InvariantCulture));
                    break;
            }
            return postfix;
        }

        private string SerializeString(string value) {
            return string.IsNullOrEmpty(value)
                       ? "N;"
                       : string.Format("s:{0}:\"{1}\";", Encoding.UTF8.GetByteCount(value), value);
        }

        private string SerializeICollection(IEnumerable enumerable, Type elementType = null) {
            StringBuilder sb = new StringBuilder();

            int i = 0;
            foreach (object val in enumerable) {
                sb.AppendFormat("i:{0};", i++);
                sb.Append(SerailizeMemberInfo(val, elementType) ?? "N;");
            }

            sb.Append("}");
            return string.Format("a:{0}:{{", i) + sb;
        }

        private string SerializeIDictionary(IDictionary value, Type valType = null) {
            StringBuilder sb = new StringBuilder();

            int i = 0;
            foreach (DictionaryEntry val in value) {
                object key = val.Key;
                if (key is int) {
                    sb.AppendFormat("i:{0};", (int) key);
                }
                else {
                    sb.AppendFormat(SerializeString(key.ToString()));
                }

                if (valType == null) {
                    sb.Append(SerailizeMemberInfo(val.Value) ?? "N;");
                }
                else {
                    sb.Append(SerailizeMemberInfo(val.Value, valType) ?? "N;");
                }
                i++;
            }

            sb.Append("}");
            return string.Format("a:{0}:{{", i) + sb;
        }
    }

}