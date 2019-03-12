using System;

using System.Collections.Generic;
using System.Text;

namespace ISC.iNet.DS.DomainModel
{
    public class InstrumentSettingsGroup
	{
		#region Fields

		private List<string> _serialNumbers = new List<string>();

		#endregion

		#region Constructors

		public InstrumentSettingsGroup( long refId, string equipmentCode, IEnumerable<string> serialNumbers, Instrument instrument )
		{
			RefId = refId;

			EquipmentCode = equipmentCode;
			EquipmentType = Device.GetDeviceType( equipmentCode );

			if ( serialNumbers != null )
				SerialNumbers.AddRange( serialNumbers );

			Instrument = instrument;
		}

		#endregion

		#region Properties

		public long RefId { get; private set; }

		/// <summary>
		/// Gets the equipment code.  Equipment code may be null.
		/// NOTE: This is desired so it can be inserted into the database as such.  
		/// Use the EquipmentType property instead for non-database operations. 
		/// </summary>
		public string EquipmentCode { get; private set; }

		/// <summary>
		/// Gets the equipment type (device type) for the equipment code of the instrument group settings.
		/// EquipmentType should NOT be Unknown if this is a Default settings group.
		/// </summary>
		public DeviceType EquipmentType { get; private set; }

		public Instrument Instrument { get; private set; }

        /// <summary>
        /// The instrument serial numbers this settings group applies to.
        /// List will be empty if this is the default settings group.
        /// </summary>
        public List<string> SerialNumbers
        {
            get
            {
                return _serialNumbers;
            }
        }

        /// <summary>
        /// Returns true if this is one of the default settings group supported by the docking station.  
		/// Otherwise, false will be returned.
        /// </summary>
        public bool Default
        {
            get { return EquipmentType != DeviceType.Unknown && SerialNumbers.Count == 0; }
		}

		#endregion
	}
}
