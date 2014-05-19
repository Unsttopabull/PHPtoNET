namespace Frost.PHPtoNET {
    interface IPHPSerializable {
        object FromPHPSerializedString(string serializedData);
    }
}
