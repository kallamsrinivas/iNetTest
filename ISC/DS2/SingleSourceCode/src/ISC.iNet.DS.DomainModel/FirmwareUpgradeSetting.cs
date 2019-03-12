namespace ISC.iNet.DS.DomainModel
{
    public class FirmwareUpgradeSetting
    {
        public string EquipmentCode { get; set; }
        public string EquipmentSubTypeCode { get; set; }
        public string EquipmentFullCode { get; set; }        
        public string Version { get; set; }
        public byte[] CheckSum { get; set; }
        public string FileName { get; set; }

        public FirmwareUpgradeSetting()
        {
        }

        public FirmwareUpgradeSetting( string equipmentCode, string equipmentSubTypeCode, string equipmentFullCode, string version, byte[] checksum, string fileName)
        {
            EquipmentCode = equipmentCode;
            EquipmentSubTypeCode = equipmentSubTypeCode;
            EquipmentFullCode = equipmentFullCode;
            Version = version;
            CheckSum = checksum;
            FileName = fileName;
        }
    }
}
