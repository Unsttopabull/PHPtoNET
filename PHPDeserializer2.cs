using System.Text;

namespace Frost.PHPtoNET {

    /// <summary>Deserializes the PHP serialize() object information.</summary>
    public static class PHPDeserializer2 {

        /// <summary>Deserializes the specified string using specified encoding.</summary>
        /// <param name="serialized">The serialied string to deserialize.</param>
        /// <param name="encoding">The encoding to use reading the string.</param>
        /// <returns></returns>
        public static object Deserialize(string serialized, Encoding encoding) {
            using (PHPSerializedStream serializedStream = new PHPSerializedStream(serialized, encoding)) {
                return Deserialize(serializedStream);
            }
        }

        /// <summary>Deserializes the specified stream.</summary>
        /// <param name="s">The stream to deserialize.</param>
        /// <returns></returns>
        /// <exception cref="ParsingException">Uknown type or malformed data detected.</exception>
        public static object Deserialize(PHPSerializedStream s) {
            switch (s.Peek()) {
                case 's':
                    return s.ReadString();
                case 'N':
                    return s.ReadNull();
                case 'i':
                    return s.ReadInteger();
                case 'd':
                    return s.ReadDouble();
                case 'b':
                    return s.ReadBoolean();
                case 'a':
                    return s.ReadArray();
                case 'O':
                    return s.ReadObject();
                default:
                    throw new ParsingException("Uknown type or malformed data detected.");
            }
        }

        /// <summary>Deserializes the specified stream as specified type.</summary>
        /// <typeparam name="T">The type to deserialize as.</typeparam>
        /// <param name="s">The stream to deserialize from.</param>
        /// <returns></returns>
        public static T Deserialize<T>(PHPSerializedStream s) {
            return s.DeserializeElement<T>();
        }

        /// <summary>Deserializes the specified string using specified encoding.</summary>
        /// <typeparam name="T">The type to deserialize to.</typeparam>
        /// <param name="serialied">The serialied string to deserialize.</param>
        /// <param name="encoding">The encoding to use reading the string.</param>
        /// <returns></returns>
        public static T Deserialize<T>(string serialied, Encoding encoding) {
            using (PHPSerializedStream serializedStream = new PHPSerializedStream(serialied, encoding)) {
                return serializedStream.DeserializeElement<T>();
            }
        }
    }

}