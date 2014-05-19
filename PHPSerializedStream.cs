using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Frost.PHPtoNET.Attributes;

namespace Frost.PHPtoNET {

    /// <summary>The stream used to parse PHP serialize() serialized format.</summary>
    public class PHPSerializedStream : IDisposable {
        private readonly Encoding _enc;
        private readonly MemoryStream _ms;
        private static Type _type = typeof(PHPSerializedStream);
        private static readonly Type StringType = typeof(string);
        private static readonly Type BoolType = typeof(bool);
        private static readonly Type DblType = typeof(double);
        private static readonly Type IntType = typeof(int);
        private static readonly Type HashTableType = typeof(Hashtable);
        private const string COMMON_LANGUAGE_RUNTIME_LIBRARY = "CommonLanguageRuntimeLibrary";

        private const BindingFlags SET_FLAGS =
            BindingFlags.GetField | BindingFlags.GetProperty | BindingFlags.Public |
            BindingFlags.Instance | BindingFlags.FlattenHierarchy |
            BindingFlags.NonPublic | BindingFlags.Static;

        /// <summary>Initializes a new instance of the <see cref="PHPSerializedStream"/> class.</summary>
        /// <param name="searializedData">The searialized data string to parse.</param>
        /// <param name="enc">The encoding used to read the data string.</param>
        public PHPSerializedStream(string searializedData, Encoding enc) : this(enc.GetBytes(searializedData), enc) {
        }

        /// <summary>Initializes a new instance of the <see cref="PHPSerializedStream"/> class.</summary>
        /// <param name="serializedData">The searialized data byte array to parse.</param>
        /// <param name="enc">The encoding used to read the data as string.</param>
        public PHPSerializedStream(byte[] serializedData, Encoding enc) {
            _enc = enc;
            _ms = new MemoryStream(serializedData);
        }

        /// <summary>Gets the position the stream is currently at.</summary>
        /// <value>The position the stream is currently at.</value>
        public long Position { get { return _ms.Position; } }

        private object InvokeGenericMethod(string methodName, Type genericArgument, params object[] methodParameters) {
            return InvokeGenericMethod(methodName, new[] { genericArgument }, methodParameters);
        }

        private object InvokeGenericMethod(string methodName, Type[] genericArguments, params object[] methodParameters) {
            if (_type == null) {
                _type = typeof(PHPSerializedStream);
            }

            MethodInfo genericArrParse = _type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).MakeGenericMethod(genericArguments);
            return genericArrParse.Invoke(this, methodParameters);
        }

        /// <summary>Reads the next character without advancing the stream.</summary>
        /// <returns></returns>
        public char Peek() {
            char peek = ReadChar();
            _ms.Position--;

            return peek;
        }

        #region Read

        /// <summary>Reads the character that is not a control character or space.</summary>
        /// <returns>A character of ASCII value greater than 32. if EOF retunrs -1.</returns>
        public char ReadChar() {
            int b = _ms.ReadByte();
            if (b == -1) {
                return (char) 0;
            }

            if (b > 32) {
                return (char) b;
            }
            return ReadChar();
        }

        /// <summary>Reads a serialized string value from the current position.</summary>
        /// <returns>The deserialized string</returns>
        /// <exception cref="ParsingException">Throws if there is no string at this position or the data is malformed.</exception>
        public string ReadString() {
            CheckString("s:");

            int byteLength = ReadIntegerValue();
            CheckChar(':');

            string readString = ReadStringContents(byteLength + 2);
            CheckChar(';');

            return readString;
        }

        private string ReadStringContents(int byteLength) {
            byte[] read = new byte[byteLength];

            int readBytes = _ms.Read(read, 0, byteLength);

            string readString = Encoding.UTF8.GetString(read);
            if (readBytes < byteLength || readBytes == 0) {
                throw new ParsingException("a string of byte lenght " + byteLength, readString, _ms.Position);
            }

            if (!string.IsNullOrEmpty(readString)) {
                readString = readString.Trim('"');
            }

            return readString;
        }

        private int ReadIntegerValue() {
            List<char> chars = new List<char>();

            while (true) {
                char read = ReadChar();

                if (read < '0' || read > '9') {
                    _ms.Position--;
                    break;
                }
                chars.Add(read);
            }

            int value;
            string strValue = new string(chars.ToArray());
            if (!int.TryParse(strValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) {
                throw new ParsingException("an integer value", strValue, _ms.Position);
            }
            return value;
        }

        private double ReadDoubleValue() {
            int numPeriods = 0;
            List<char> chars = new List<char>();

            while (true) {
                char read = ReadChar();

                if ((read < '0' || read > '9' || numPeriods >= 2) && read != '.') {
                    _ms.Position--;
                    break;
                }

                if (read == '.') {
                    numPeriods++;
                }

                chars.Add(read);
            }

            double value;
            string strValue = new string(chars.ToArray());
            if (!double.TryParse(strValue, NumberStyles.Float, CultureInfo.InvariantCulture ,out value)) {
                throw new ParsingException("an integer value", strValue, _ms.Position);
            }
            return value;
        }

        private void CheckChar(char expected) {
            char read = ReadChar();
            if (read != expected) {
                throw new ParsingException(expected.ToString(CultureInfo.InvariantCulture), read.ToString(CultureInfo.InvariantCulture), _ms.Position);
            }
        }

        private void CheckString(string expected) {
            byte[] str = new byte[expected.Length];
            _ms.Read(str, 0, expected.Length);

            string readString = _enc.GetString(str);
            if (string.Compare(expected, readString, StringComparison.Ordinal) != 0) {
                throw new ParsingException("\"" + expected + "\"", readString, _ms.Position);
            }
        }

        /// <summary>Reads a serialized null value from the stream.</summary>
        /// <returns>A null value</returns>
        /// <exception cref="ParsingException">Throws if null does not exists at this position or the data is malformed.</exception>
        public object ReadNull() {
            CheckString("N;");
            return null;
        }

        /// <summary>Reads a serialized integer value from the stream.</summary>
        /// <returns>The deserialized integer value</returns>
        /// <exception cref="ParsingException">Throws if integer does not exists at this position or the data is malformed.</exception>
        public int ReadInteger() {
            CheckString("i:");

            int val = ReadIntegerValue();
            CheckChar(';');

            return val;
        }

        /// <summary>Reads a serialized double/long value from the stream.</summary>
        /// <returns>The deserialized double/long value</returns>
        /// <exception cref="ParsingException">Throws if double/long does not exists at this position or the data is malformed.</exception>
        public double ReadDouble() {
            CheckString("d:");

            double val = ReadDoubleValue();

            CheckChar(';');

            return val;
        }

        /// <summary>Reads a serialized boolean value from the stream.</summary>
        /// <returns>The deserialized boolean value</returns>
        /// <exception cref="ParsingException">Throws if boolean does not exists at this position or the data is malformed.</exception>
        public bool ReadBoolean() {
            CheckString("b:");

            char bVal = ReadChar();
            bool b;
            if (bVal == '1') {
                b = true;
            }
            else if (bVal == '0') {
                b = false;
            }
            else {
                throw new ParsingException("boolean value of '1' or '0'", bVal.ToString(CultureInfo.InvariantCulture), _ms.Position);
            }

            CheckChar(';');

            return b;
        }

        /// <summary>Reads a serialized array of values from the stream (can be mixed type).</summary>
        /// <returns>The deserialized array.</returns>
        /// <exception cref="ParsingException">Throws if an array does not exists at this position or the data is malformed.</exception>
        public IEnumerable ReadArray() {
            CheckString("a:");
            int len = ReadIntegerValue();
            CheckString(":{");

            Hashtable ht = null;
            if (Peek() == 's') {
                ht = ReadDictionaryElements(len);
            }

            if (ht != null) {
                CheckChar('}');
                return ht;
            }

            long pos = _ms.Position;
            object[] arr;
            if (!ReadArrayElements(len, out arr)) {
                _ms.Seek(pos, SeekOrigin.Begin);

                ht = ReadDictionaryElements(len);
                CheckChar('}');

                throw new ParsingException("An error has occured while parsing an array. The serialized data is probably malformed.");
            }

            CheckChar('}');
            return arr;
        }

        /// <summary>Reads a serialized array of single type values from the stream.</summary>
        /// <returns>The deserialized single-type array.</returns>
        /// <exception cref="ParsingException">Throws if an array does not exists at this position or the data is malformed.</exception>
        public object[] ReadSingleTypeArray() {
            CheckString("a:");
            int len = ReadIntegerValue();
            CheckString(":{");

            object[] arr;
            ReadArrayElements(len, out arr);

            CheckChar('}');

            return arr;
        }

        /// <summary>Reads a key-value array where the keys can be string or integer (can mix).</summary>
        /// <returns>The deserialized key-value array.</returns>
        /// <exception cref="ParsingException">Throws if an array does not exists at this position or the data is malformed.</exception>
        public Hashtable ReadAsociativeArray() {
            CheckString("a:");
            int len = ReadIntegerValue();
            CheckString(":{");

            Hashtable ht = ReadDictionaryElements(len);

            CheckChar('}');

            return ht;
        }

        private Hashtable ReadDictionaryElements(int len) {
            Hashtable ht = new Hashtable();
            for (int i = 0; i < len; i++) {
                object key = ReadKey();
                object value = ReadElement();
                ht.Add(key, value);
            }

            return ht;
        }

        private object ReadKey() {
            char peek = Peek();
            if (peek == 's') {
                return ReadString();
            }

            if (peek == 'i') {
                return ReadInteger();
            }
            throw new ParsingException("an integer or string key", peek.ToString(CultureInfo.InvariantCulture), _ms.Position);
        }

        private bool ReadArrayElements(int len, out object[] arr) {
            object[] elements = new object[len];

            for (int i = 0; i < len; i++) {
                int idx = ReadInteger();

                if (idx < 0 || idx > len) {
                    arr = null;
                    return false;
                }
                elements[idx] = ReadElement();
            }

            arr = elements;
            return true;
        }

        private object ReadElement() {
            switch (Peek()) {
                case 's':
                    return ReadString();
                case 'N':
                    return ReadNull();
                case 'i':
                    return ReadInteger();
                case 'd':
                    return ReadDouble();
                case 'b':
                    return ReadBoolean();
                case 'a':
                    return ReadArray();
                case 'O':
                    return ReadObject();
                default:
                    throw new ParsingException("Uknown type or malformed data detected.");
            }
        }

        /// <summary>Reads serailized object form the steam as a dynamicaly consturcted object.</summary>
        /// <returns>An ExpandoObject dynamicly consturcted from the serialized data.</returns>
        /// <exception cref="ParsingException">Throws if an object does not exists at this position or the data is malformed.</exception>
        public dynamic ReadObject() {
            CheckString("O:");
            int nameByteLenght = ReadIntegerValue();
            CheckChar(':');

            string objName = ReadStringContents(nameByteLenght + 2);
            CheckChar(':');

            int numFields = ReadIntegerValue();
            CheckString(":{");

            dynamic dyn = new ExpandoObject();
            dyn.__ClassName = objName;

            for (int i = 0; i < numFields; i++) {
                string key = ReadString();
                object value = ReadElement();

                ((IDictionary<string, object>) dyn).Add(key, value);
            }

            CheckChar('}');

            return dyn;
        }

        #endregion

        #region Deserialize

        internal T DeserializeElement<T>(Type type = null) {
            Type t = type ?? typeof(T);

            //Debug.WriteLine("Deserializing element with expected type: " + t.FullName);
            //Debug.Indent();

            object obj;
            switch (Peek()) {
                case 's': {
                    //Debug.WriteLine("Found string:");

                    if (t == StringType || (t.FullName == "System.Object" && t.Module.ScopeName == COMMON_LANGUAGE_RUNTIME_LIBRARY)) {
                        obj = ReadString();

                        //Debug.WriteLine("Finished deserializing string as String.");
                        //Debug.Unindent();
                        return (T) obj;
                    }

                    if (t.IsValueType) {
                        T tObj = DeserializeStructFromString<T>(t);

                        //Debug.WriteLine("Finished deserializing string as struct.");
                        //Debug.Unindent();
                        return tObj;
                    }

                    throw new ParsingException(t, "a serialized string", _ms.Position);
                }
                case 'N': {
                    //Debug.WriteLine("Found null:");
                    if (t.IsClass || (t.Name == "Nullable`1" && t.Namespace == "System" && t.Module.ScopeName == COMMON_LANGUAGE_RUNTIME_LIBRARY)) {
                        obj = ReadNull();

                        T tObj = (T) obj;

                        //Debug.WriteLine("Finished deserializing null.");
                        //Debug.Unindent();
                        return tObj;
                    }
                    throw new ParsingException(t, "a nullable type", _ms.Position);
                }
                case 'i': {
                    //Debug.WriteLine("Found integer:");
                    //Debug.Indent();

                    T tObj = DeserializeInteger<T>(t);

                    //Debug.Unindent();
                    //Debug.WriteLine("Finished deserializing integer.");
                    //Debug.Unindent();
                    return tObj;
                }
                case 'd': {
                    //Debug.WriteLine("Found double:");
                    //Debug.Indent();

                    T tObj = DeserializeDouble<T>(t);

                    //Debug.Unindent();
                    //Debug.WriteLine("Finished deserializing double.");
                    //Debug.Unindent();
                    return tObj;
                }
                case 'b': {
                    //Debug.WriteLine("Found bool:");

                    if (t == BoolType) {
                        obj = ReadBoolean();
                        T tObj = (T) obj;

                        //Debug.WriteLine("Finished deserializing bool");
                        //Debug.Unindent();
                        return tObj;
                    }
                    throw new ParsingException(t, "a serialized boolean", _ms.Position);
                }
                case 'a': {
                    //Debug.WriteLine("Found array:");
                    //Debug.Indent();

                    T tObj = DeserializePHPArray<T>(t);

                    //Debug.Unindent();
                    //Debug.WriteLine("Finished deserializing Array");
                    //Debug.Unindent();
                    return tObj;
                }
                case 'O': {
                    //Debug.WriteLine("Found object:");
                    //Debug.Indent();

                    T tObj = DeserializeObject<T>(t);

                    //Debug.Unindent();
                    //Debug.WriteLine("Finished deserializing Object");
                    //Debug.Unindent();
                    return tObj;
                }
                default:
                    throw new ParsingException("Uknown type or malformed data detected.");
            }
        }

        private T DeserializeStructFromString<T>(Type t) {
            string str = ReadString();

            if (t.GetInterface("IPHPSerializable") != null) {
                IPHPSerializable instance = (IPHPSerializable) Activator.CreateInstance(t);
                object obj = instance.FromPHPSerializedString(str);
                return (T) obj;
            }

            MethodInfo mi = t.GetMethod("TryParse", new[] { StringType, typeof(NumberStyles), typeof(IFormatProvider), t.MakeByRefType() });
            if (mi != null) {
                object[] args = { str, NumberStyles.Any, CultureInfo.InvariantCulture, null };
                if ((bool) mi.Invoke(null, args)) {
                    return (T) args[3];
                }
                throw new ParsingException(string.Format("The TryParse(string, NumberStyles, IFormatProvider, out T) method failed when deserializing type: '{0}'", t.FullName));
            }

            mi = t.GetMethod("TryParse", new[] { StringType, t.MakeByRefType() });
            if (mi != null) {
                object[] args = { str, null };

                if ((bool) mi.Invoke(null, args)) {
                    return (T) args[1];
                }
                throw new ParsingException(string.Format("The TryParse(string, out T) method failed when deserializing type: '{0}'", t.FullName));
            }

            throw new ParsingException(t, 
                "the type that does not implement 'IPHPSerializable' or has a static method 'TryParse(string, NumberStyles, IFormatProvider, out T)' that returns bool.",
                _ms.Position
            );
        }

        private T DeserializeObject<T>(Type type) {
            CheckString("O:");
            int nameByteLenght = ReadIntegerValue();
            CheckChar(':');

            string objName = ReadStringContents(nameByteLenght + 2);
            CheckChar(':');

            int numFields = ReadIntegerValue();
            CheckString(":{");

            //Debug.WriteLine("Reading object with name: " + objName);
            //Debug.WriteLine("Number of fields " + numFields);

            T obj = (T) Activator.CreateInstance(type);

            MemberInfo[] members = type.GetMembers(SET_FLAGS)
                                       .Where(mi => mi.MemberType != MemberTypes.Method &&
                                                    mi.MemberType != MemberTypes.Constructor &&
                                                    mi.MemberType != MemberTypes.NestedType &&
                                                    mi.MemberType != MemberTypes.TypeInfo)
                                       .ToArray();

            for (int i = 0; i < numFields; i++) {
                //Debug.WriteLine("Starting to deserialize object member: " + i);
                //Debug.Indent();

                string fieldName = ReadString();

                //Debug.WriteLine("Member name: " + fieldName);
                MemberInfo member = Array.Find(members, mi => GetMemberByName(mi, fieldName));

                if (member != null) {
                    DeserializeObjectMember(obj, member);
                }
                else {
                    //the member with the same name does not exits
                    //read but do nothing with the output
                    //Debug.WriteLine("Member missing on class.");
                    ReadElement();
                }

                //Debug.Unindent();
            }

            CheckChar('}');

            return obj;
        }

        private bool GetMemberByName(MemberInfo mi, string fieldName) {
            if (mi.Name == fieldName) {
                return true;
            }

            if (mi.IsDefined(typeof(PHPNameAttribute), false)) {
                string customName = ((PHPNameAttribute) mi.GetCustomAttributes(typeof(PHPNameAttribute), false)[0]).PHPName;
                if (fieldName == customName) {
                    return true;
                }
            }
            return false;
        }

        private void DeserializeObjectMember<T>(T obj, MemberInfo member) {
            if (member.MemberType == MemberTypes.Field) {
                FieldInfo fieldInfo = (FieldInfo) member;
                Type memberType = fieldInfo.FieldType;

                //Debug.WriteLine("Member type: Field");
                //Debug.Indent();

                object value = InvokeGenericMethod("DeserializeElement", memberType, memberType);

                //Debug.Unindent();
                //Debug.WriteLine("Succesfully deserialized member");

                fieldInfo.SetValue(obj, value);
            }
            else if (member.MemberType == MemberTypes.Property) {
                PropertyInfo propertyInfo = (PropertyInfo) member;
                Type memberType = propertyInfo.PropertyType;

                //Debug.WriteLine("Member type: Property");
                //Debug.Indent();

                object value = InvokeGenericMethod("DeserializeElement", new[] { memberType }, memberType);

                //Debug.Unindent();
                //Debug.WriteLine("Succesfully deserialized member");

                propertyInfo.SetValue(obj, value, null);
            }
        }

        private T DeserializePHPArray<T>(Type type) {
            //Debug.WriteLine("Deserializing array with type: " + type.FullName);

            if (type.IsArray) {
                //Debug.WriteLine("Array is a plain array");

                //Debug.WriteLine("Starting to deserialize array.");
                //Debug.Indent();

                T tObj = (T) InvokeGenericMethod("DeserializeArray", type.GetElementType());

                //Debug.Unindent();
                //Debug.WriteLine("Finished Deserializing array.");
                return tObj;
            }

            if (type.IsGenericType && type.GetInterface("IList`1") != null) {
                //Debug.WriteLine("Array is a generic list");

                //Debug.WriteLine("Starting to deserialize generic list.");
                //Debug.Indent();

                T tObj = DeserializeGenericList<T>(type);

                //Debug.Unindent();
                //Debug.WriteLine("Finished Deserializing array.");
                return tObj;
            }

            if (type.GetInterface("IList") != null) {
                //Debug.WriteLine("Array is an IList");

                //Debug.WriteLine("Starting to deserialize an IList.");
                //Debug.Indent();

                T tObj = DeserializeList<T>(type);

                //Debug.Unindent();
                //Debug.WriteLine("Finished Deserializing array.");
                return tObj;
            }

            if (type.IsGenericType && type.GetInterface("IDictionary`2") != null) {
                //Debug.WriteLine("Array is an generic dictionary");

                //Debug.WriteLine("Starting to deserialize a generic dictionary.");
                //Debug.Indent();

                T tObj = DeserializeGenericDictionary<T>(type);

                //Debug.Unindent();
                //Debug.WriteLine("Finished desrializing generic dictionary.");
                return tObj;
            }

            if (type.GetInterface("IDictionary") != null) {
                //Debug.WriteLine("Array is an IDictionary");

                //Debug.WriteLine("Starting to deserialize an IDictionary.");
                //Debug.Indent();

                Hashtable ht = ReadAsociativeArray();
                T tObj;
                if (type == HashTableType) {
                    tObj = ht.CastAs<T>();

                    //Debug.WriteLine("Array is a hashtable");
                    //Debug.Unindent();
                    //Debug.WriteLine("Finished desrializing the hashtable.");
                    return tObj;
                }

                //Debug.WriteLine("Converting Hashtable to the " + type.FullName);

                IDictionary dict = (IDictionary) Activator.CreateInstance(type);
                foreach (DictionaryEntry entry in ht) {
                    dict.Add(entry.Key, entry.Value);
                }

                tObj = (T) dict;

                //Debug.WriteLine("Finished converting.");
                //Debug.Unindent();
                //Debug.WriteLine("Finished desrializing the hashtable.");
                return tObj;
            }
            return default(T);
        }

        private T DeserializeGenericDictionary<T>(Type type) {
            Type[] genericArguments = type.GetGenericArguments();

            //Debug.WriteLine("Deserializing generic dictionary with expected type " + type.FullName);
            //Debug.Indent();

            IDictionary genericDict = (IDictionary) InvokeGenericMethod("DeserializeDictionary", genericArguments);

            //Debug.Unindent();
            //Debug.WriteLine("Finished deserializing.");

            //Debug.WriteLine("Converting generic IDictionary to type: " + type.FullName);

            object instance = Activator.CreateInstance(type);

            MethodInfo addToDict = type.GetMethod("Add");
            foreach (DictionaryEntry entry in genericDict) {
                addToDict.Invoke(instance, new[] { entry.Key, entry.Value });
            }

            //Debug.WriteLine("Finished converting.");

            return (T) instance;
        }

        private T DeserializeList<T>(Type t) {
            //Debug.WriteLine("Deserializing as an object array.");
            //Debug.Indent();

            object[] arr = DeserializeArray<object>();

            //Debug.Unindent();
            //Debug.WriteLine("Finished deserializing object array.");

            //Debug.WriteLine("Converting to: " + t.FullName);

            IList list = (IList) Activator.CreateInstance(t);
            foreach (object value in arr) {
                list.Add(value);
            }

            //Debug.WriteLine("Finished converting");
            return (T) list;
        }

        private T DeserializeGenericList<T>(Type t) {
            Type[] genericArguments = t.GetGenericArguments();

            //Debug.WriteLine("Deserializing generic list of type: {0} with elements of type: {1}", t.FullName, genericArguments[0]);
            //Debug.Indent();

            object[] arr = (object[]) InvokeGenericMethod("DeserializeArray", genericArguments);

            //Debug.Unindent();
            //Debug.WriteLine("Finished deserializing generic list");

            object genericList = Activator.CreateInstance(t);

            //Debug.WriteLine("Coverting generic IList to type: " + t.FullName);

            MethodInfo addToList = t.GetMethod("Add");
            foreach (object element in arr) {
                addToList.Invoke(genericList, new[] { element });
            }

            //Debug.WriteLine("Finished coverting generic IList");

            return (T) genericList;
        }

        private T DeserializeInteger<T>(Type type) {

            object obj;
            if (type.Name == "Nullable`1") {
                Type nullableType = type.GetGenericArguments()[0];
                obj = ReadInteger();
                if (nullableType != IntType) {
                    try {
                        obj = Convert.ChangeType(obj, nullableType);
                    }
                    catch (Exception) {
                        throw new ParsingException("Could not convert integer to the type " + type.FullName);
                    }
                }

                return (T) Activator.CreateInstance(type, obj);
            }
            obj = ReadInteger();

            if (type != IntType) {
                obj = Convert.ChangeType(obj, type);
            }
            return (T) obj;
        }

        private T DeserializeDouble<T>(Type type) {
            object obj;
            if (type.Name == "Nullable`1") {
                Type nullableType = type.GetGenericArguments()[0];
                obj = ReadDouble();
                if (nullableType != DblType) {
                    try {
                        obj = Convert.ChangeType(obj, nullableType);
                    }
                    catch (Exception e) {
                        throw new ParsingException(string.Format("Could not convert double to the type {0}({1})", type.FullName, e.Message));
                    }
                }

                return (T) Activator.CreateInstance(type, obj);
            }
            obj = ReadDouble();

            if (type != DblType) {
                obj = Convert.ChangeType(obj, type);
            }
            return (T) obj;
        }

        /// <summary>Reads a serialized array of single type values from the stream.</summary>
        /// <typeparam name="TElement">The type of the array elements.</typeparam>
        /// <returns>The deserialized single-type array.</returns>
        /// <exception cref="ParsingException">Throws if an array does not exists at this position or the data is malformed.</exception>
        public TElement[] DeserializeArray<TElement>() {
            CheckString("a:");
            int len = ReadIntegerValue();
            CheckString(":{");

            //Debug.WriteLine("Deserializing array with expected elements of type " + typeof(TElement).FullName);
            //Debug.Indent();

            TElement[] arr = DeserializeArrayElements<TElement>(len);

            //Debug.Unindent();
            //Debug.WriteLine("Finished deserializing array");

            CheckChar('}');

            return arr;
        }

        /// <summary>Reads a serialized key-value array from the stream.</summary>
        /// <typeparam name="TKey">The type of the key (can be string or integral type).</typeparam>
        /// <typeparam name="TValue">The type of the value (can be mixed if set to <see cref="Object"/>).</typeparam>
        /// <returns>The deserialized key-value array.</returns>
        /// <exception cref="ParsingException">Throws if the key-value array does not exists at this position or the data is malformed.</exception>
        public IDictionary<TKey, TValue> DeserializeDictionary<TKey, TValue>() {
            CheckString("a:");
            int len = ReadIntegerValue();
            CheckString(":{");

            //Debug.WriteLine("Deserializing dictionary with expected elements of type {0} and keys of type {1}", typeof(TValue).FullName, typeof(TKey).FullName);
            //Debug.Indent();

            IDictionary<TKey, TValue> dict = DeserializeDictionaryElements<TKey, TValue>(len);

            //Debug.Unindent();
            //Debug.WriteLine("Finished deserializing dictionary");

            CheckChar('}');

            return dict;
        }

        private IDictionary<TKey, TValue> DeserializeDictionaryElements<TKey, TValue>(int len) {
            IDictionary<TKey, TValue> dict = new Dictionary<TKey, TValue>();
            for (int i = 0; i < len; i++) {
                //Debug.WriteLine("Deserializing dictionary entry key.");
                //Debug.Indent();

                TKey key = DeserializeKey<TKey>();

                //Debug.Unindent();
                //Debug.WriteLine("Finshed deserializing dictionary entry with value: " + key);

                //Debug.WriteLine("Deserializing dictionary entry value.");
                //Debug.Indent();

                TValue value = DeserializeElement<TValue>();

                //Debug.Unindent();
                //Debug.WriteLine("Finshed deserializing dictionary entry value");

                dict.Add(key, value);
            }

            return dict;
        }

        private TElement[] DeserializeArrayElements<TElement>(int len) {
            TElement[] elements = new TElement[len];

            for (int i = 0; i < len; i++) {
                int idx = ReadInteger();

                if (idx < 0 || idx > len) {
                    return null;
                }

                //Debug.WriteLine("Deserializing element with index: " + idx);
                //Debug.Indent();

                elements[idx] = DeserializeElement<TElement>();

                //Debug.Unindent();
                //Debug.WriteLine("Finished deserializing element with index: " + idx);
            }
            return elements;
        }

        private T DeserializeKey<T>() {
            char peek = Peek();

            if (peek == 'i') {
                return ReadInteger().CastAs<T>();
            }

            if (peek == 's' && typeof(T) == StringType) {
                return ReadString().CastAs<T>();
            }
            throw new ParsingException("an integer or string key ('i' or 's')", peek.ToString(CultureInfo.InvariantCulture), _ms.Position);
        }

        #endregion

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose() {
            if (_ms != null) {
                _ms.Dispose();
            }
        }
    }

}