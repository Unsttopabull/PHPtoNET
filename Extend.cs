namespace Frost.PHPtoNET {
    static class Extend {
        public static T CastAs<T>(this object ex){
            return (T) ex;
        }
    }
}
