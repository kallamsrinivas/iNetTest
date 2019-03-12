using System;
using System.Collections.Generic;
using System.Text;

namespace ISC.iNet.DS.DomainModel
{
	/// <summary>
	/// TODO: WirelessModule needs to be made a subclass of Component class.
	/// </summary>
	public class WirelessModule : ICloneable
	{
		#region Fields

		private string _macAddress;
		private string _softwareVersion;
		private string _status;

		private string _hardwareVersion;
		private string _radioHardwareVersion;
		private string _osVersion;
		private string _encryptionKey;
		private string _activeChannelMask;
        private string _wirelessFeatureBits;
        private string _listeningPostChannelMask;

		List<DeviceOption> _wirelessOptions;

		#endregion

		#region Constructors

		/// <summary>
		/// Used for settings downloaded from iNet.
		/// </summary>
		public WirelessModule()
		{

		}

		/// <summary>
		/// Used for values read from the instrument.
		/// </summary>
		public WirelessModule( string macAddress, string softwareVersion, string status, int transmissionInterval )
		{
			MacAddress = macAddress;
			SoftwareVersion = softwareVersion;
			Status = status;
			TransmissionInterval = transmissionInterval;
		}

		#endregion

		#region Properties

		/// <summary>
		/// If the MAC Address is an empty string than no wireless 
		/// module is installed in the docked instrument.
		/// </summary>
		public string MacAddress 
		{ 
			get
			{
				if ( _macAddress == null )
					_macAddress = string.Empty;

				return _macAddress;
			}
			private set
			{
				_macAddress = value;
			}
		}
		public string SoftwareVersion
		{
			get
			{
				if ( _softwareVersion == null )
					_softwareVersion = string.Empty;

				return _softwareVersion;
			}
			private set
			{
				_softwareVersion = value;	
			} 
		}
		public string Status 
		{
			get
			{
				if ( _status == null )
					_status = string.Empty;

				return _status;
			}
			private set
			{
				_status = value;
			}
		}
		public int TransmissionInterval { get; set; }

		// New Wireless Module Properties
		public string HardwareVersion 
		{
			get
			{
				if ( _hardwareVersion == null )
					_hardwareVersion = string.Empty;

				return _hardwareVersion;
			}
			set
			{
				_hardwareVersion = value;
			}
		}
		public string RadioHardwareVersion 
		{
			get
			{
				if ( _radioHardwareVersion == null )
					_radioHardwareVersion = string.Empty;

				return _radioHardwareVersion;
			}
			set
			{
				_radioHardwareVersion = value;
			}
		}
		public string OsVersion 
		{ 
			get
			{
				if ( _osVersion == null )
					_osVersion = string.Empty;

				return _osVersion;
			}
			set
			{
				_osVersion = value;
			}
		}

		public string EncryptionKey 
		{ 
			get
			{
				if ( _encryptionKey == null )
					_encryptionKey = string.Empty;

				return _encryptionKey;
			}
			set
			{
				_encryptionKey = value;
			}
		}

        public string WirelessFeatureBits
        {
            get
            {
                if (_wirelessFeatureBits == null)
                    _wirelessFeatureBits = string.Empty;

                return _wirelessFeatureBits;
            }
            set
            {
                _wirelessFeatureBits = value;
            }
        }

		public int MessageHops { get; set; }
		public int MaxPeers { get; set; }
		public ushort PrimaryChannel { get; set; }
		public ushort SecondaryChannel { get; set; }
		public string ActiveChannelMask
		{
			get
			{
				if ( _activeChannelMask == null )
					_activeChannelMask = string.Empty;

				return _activeChannelMask;
			}
			set
			{
				_activeChannelMask = value;
			}
		}

        /// <summary>
        /// The Wireless Script Binding Timeout in seconds
        /// </summary>
        public int WirelessBindingTimeout { get; set; }

		/// <summary>
		/// The list of instrument driver options that have been categorized
		/// to apply to the wireless module group.
		/// </summary>
		public List<DeviceOption> Options
		{
			get
			{
				if ( _wirelessOptions == null )
					_wirelessOptions = new List<DeviceOption>();

				return _wirelessOptions;
			}
			set
			{
				_wirelessOptions = value;
			}
		}

        /// <summary>
        /// Gets or sets the Listening Post Channel Mask.
        /// </summary>
        public string ListeningPostChannelMask 
        {
            get
            {
                if (_listeningPostChannelMask == null)
                    _listeningPostChannelMask = string.Empty;

                return _listeningPostChannelMask;
            }
            set
            {
                _listeningPostChannelMask = value;
            }
        }

		#endregion

		#region Methods

		/// <summary>
		/// Implementation of ICloneable::Clone.
		/// </summary>
		/// <returns>Cloned WirelessModule</returns>
		public object Clone()
		{
			WirelessModule module = (WirelessModule)this.MemberwiseClone();

			module.Options = new List<DeviceOption>( this.Options.Count );
			foreach ( DeviceOption deviceOption in Options )
				module.Options.Add( (DeviceOption)deviceOption.Clone() );

			return module;
		}

		#endregion
	}
}
