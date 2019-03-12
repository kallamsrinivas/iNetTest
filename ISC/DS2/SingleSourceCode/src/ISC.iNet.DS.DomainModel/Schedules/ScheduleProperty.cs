namespace ISC.iNet.DS.DomainModel
{
    public class ScheduleProperty
    {
        public const string FirmwareUpgradeVersion = "FIRMWAREUPGRADEVERSION";
        public const string ATTR_ALLOWBUMPAFTERCAL = "ALLOWBUMPAFTERCAL";
        
        public long ScheduleId { get; set; }
        public string Attribute { get; set; }
        public string Value { get; set; }
        public short Sequence { get; set; }

        public ScheduleProperty()
        {
        }

        public ScheduleProperty( long scheduleId, string attribute, short sequence, string value )
        {
            ScheduleId = scheduleId;
            Attribute = attribute;
            Sequence = sequence;
            Value = value;
        }        
    }
}
