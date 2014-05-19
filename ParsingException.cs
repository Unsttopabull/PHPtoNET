#region

using System;

#endregion

namespace Frost.PHPtoNET {
    internal class ParsingException : Exception {
        private const string MSG = "Error occured while parsing, expected \"{0}\", found \"{1}\" on column {2}.";
        private const string MSG2 = "Expceted value of type {0} got {1}";
        private readonly long _column;
        private readonly bool _custom;
        private readonly string _customMessage = "";
        private readonly string _expected;
        private readonly bool _msg;
        private readonly string _read;
        private readonly string[] _typeNames;

        public ParsingException(Type expectedType, string read, long column) {
            _read = read;
            _column = column;
            _expected = string.Format("a serialized type '{0}'", expectedType.Name);
            _msg = true;
            _typeNames = null;            
        }

        public ParsingException(string expected, string read, long column) {
            _read = read;
            _column = column;
            _expected = expected;
            _msg = true;
            _typeNames = null;
        }

        public ParsingException(string customMessage) {
            _custom = true;
            _customMessage = customMessage;
        }

        public override string Message {
            get {
                if (!_custom) {
                    return _msg
                        ? string.Format(MSG, _expected, _read, _column)
                        : string.Format(MSG2, _typeNames[0], _typeNames[1]);
                }
                return _customMessage;
            }
        }
    }
}
