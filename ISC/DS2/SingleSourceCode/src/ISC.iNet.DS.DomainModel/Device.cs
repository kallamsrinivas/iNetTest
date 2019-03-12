using System;
using System.Collections.Generic;
using ISC.WinCE;


namespace ISC.iNet.DS.DomainModel
{

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides base functionality to specific device related classes.
	/// </summary>
	public abstract class Device : ICloneable
	{
		#region Fields

        static Dictionary<DeviceType,DeviceType> _dockableTypes;

		private string _serialNumber;
		private string _partNumber;
		private string _setupTech;
		private string _softwareVersion;
		private string _bootloaderVersion;
        private string _hardwareVersion;
		private int _operationMinutes;
		private DateTime _setupDate;
        private string   _setupVersion;
		private DeviceType _type;
		private DeviceSubType _subtype;
		private List<DeviceOption> _deviceOptions;
        private Language _language;
        private long _refId = DomainModelConstant.NullId;

		#endregion

		#region Constructors

        static Device()
        {
            _dockableTypes = new Dictionary<DeviceType, DeviceType>();

            _dockableTypes.Add( DeviceType.GBPLS, DeviceType.GBPLS );
            _dockableTypes.Add( DeviceType.GBPRO, DeviceType.GBPRO );
            _dockableTypes.Add( DeviceType.MX4, DeviceType.MX4 );
            _dockableTypes.Add( DeviceType.MX6, DeviceType.MX6 );
            _dockableTypes.Add( DeviceType.TX1, DeviceType.TX1 );
        }

		/// <summary>
		/// Creates a new instance of Device class.
		/// </summary>
		public Device()
		{
			Initialize();
		}

		/// <summary>
		/// Creates a new instance of Device class when device serial number is provided.
		/// </summary>
		/// <param name="serialNumber">Device serial number</param>
		public Device( string serialNumber )
		{
			Initialize();
			SerialNumber = serialNumber;
		}

        /// <summary>
        /// This method initializes local variables and is called by the constructors of the class.
        /// </summary>
        private void Initialize()
        {
            OperationMinutes = DomainModelConstant.NullInt;
            SetupVersion = string.Empty;
        }

		#endregion

		#region Properties

        /// <summary>
		/// Gets or sets the device serial number.
		/// </summary>
		public string SerialNumber
		{
			get
			{
				if ( _serialNumber == null )
				{
					_serialNumber = string.Empty;
				}

				return _serialNumber;
			}
			set
			{
				if ( value == null )
				{
					_serialNumber = null;
				}
				else
				{
					_serialNumber = value.Trim().ToUpper();
				}
			}
		}

        /// <summary>
        /// The refId of the settings
        /// </summary>
        public long RefId
        {
            get { return _refId; }
            set { _refId = value; }
        }

		/// <summary>
		/// Gets or sets the device part number.
		/// </summary>
		public string PartNumber
		{
			get
			{
				if ( _partNumber == null )
				{
					_partNumber = string.Empty;
				}

				return _partNumber;
			}
			set
			{
				if ( value == null )
				{
					_partNumber = null;
				}
				else
				{
					_partNumber = value.Trim().ToUpper();
				}
			}
		}

		/// <summary>
		/// Gets or sets the device setup date.
		/// </summary>
		public DateTime SetupDate
		{
			get
			{
				return _setupDate;
			}
			set
			{
				_setupDate = value.Date;
			}
		}

		/// <summary>
		/// Gets or sets the device setup technician.
		/// </summary>
		public string SetupTech
		{
			get
			{
				if ( _setupTech == null )
				{
					_setupTech = string.Empty;
				}

				return _setupTech;
			}
			set
			{
				if ( value == null )
				{
					_setupTech = null;
				}
				else
				{
					_setupTech = value.Trim();
				}
			}
		}

        /// <summary>
        /// Gets or Sets the version number used by setup software to
        /// configure device
        /// </summary>
        public string SetupVersion
        {
            get
            {
                return _setupVersion;
            }
            set
            {
                _setupVersion = ( value == null ) ? string.Empty : value.Trim().ToUpper();
            }
        }

        /// <summary>
        /// Gets or sets the language of the docking station or instrument.
        /// </summary>
        public Language Language
        {
            get
            {
                if ( _language == null )
                {
                    _language = new Language();
                }

                return _language;
            }
            set
            {
                _language = value;
            }
        }

		/// <summary>
		/// Gets or sets the device type.
		/// </summary>
		public DeviceType Type
		{
			get
			{
				return _type;
			}
			set
			{
				_type = value;
			}
		}

		public DeviceSubType Subtype
		{
			get
			{
				return _subtype;
			}
			set
			{
				_subtype = value;
			}
		}

		/// <summary>
		/// Gets or sets the device software version.
		/// </summary>
		public string SoftwareVersion
		{
			get
			{
				if ( _softwareVersion == null )
				{
					_softwareVersion = string.Empty;
				}

				return _softwareVersion;
			}
			set
			{
				if ( value == null )
				{
					_softwareVersion = null;
				}
				else
				{
					_softwareVersion = value.Trim().ToUpper();
				}
			}
		}

		/// <summary>
		/// Gets or sets the device bootloader version.
		/// </summary>
		public string BootloaderVersion
		{
			get
			{
				if ( _bootloaderVersion == null )
				{
					_bootloaderVersion = string.Empty;
				}

				return _bootloaderVersion;
			}
			set
			{
				if ( value == null )
				{
					_bootloaderVersion = null;
				}
				else
				{
					_bootloaderVersion = value.Trim().ToUpper();
				}
			}
		}

        /// <summary>
        /// Gets or sets the device's hardware version.
        /// </summary>
        public string HardwareVersion
        {
            get
            {
                return ( _hardwareVersion == null ) ? string.Empty : _hardwareVersion;
            }
            set
            {
                _hardwareVersion = (value == null) ? null : value.Trim().ToUpper();
            }
        }

		/// <summary>
		/// Gets or sets the device minutes of operation.
		/// </summary>
		public int OperationMinutes
		{
			get
			{
				return _operationMinutes;
			}
			set
			{
				_operationMinutes = value;
			}
		}

		/// <summary>
		/// Gets or sets the list of options supported by the device.
		/// </summary>
		public List<DeviceOption> Options
		{
			get
			{
				if ( _deviceOptions == null )
                    _deviceOptions = new List<DeviceOption>();

				return _deviceOptions;
			}
            set
            {
                _deviceOptions = value;
            }
		}

		#endregion  // Properties

		#region Methods

        /// <summary>
        /// Returns whether or not the specified DeviceType is for an 
        /// instrument that can be recharged or not.
        /// (Does not take into account whether the instrument has
        /// an alkaline battery or not. Assumes not.)
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <param name="dt"></param>
        /// <returns></returns>
        public static bool IsRechargeable( DeviceType dt )
        {
			return dt == DeviceType.MX4 || dt == DeviceType.MX6 || dt == DeviceType.VPRO;
        }

        /// <summary>
        /// Returns whether or not the instrument is of a type that can be recharged or not.
        /// <para>
        /// (It does not take into account
        /// whether the instrument currently has an alkaline battery or rechargeable
        /// battery.)
        /// </para>
        /// </summary>
        /// <returns></returns>
        public bool IsRechargeable()
        {
            return IsRechargeable( this.Type );
        }

		/// <summary>
		/// Returns the equivalent DeviceType enum value for a provided equipment code string. 
		/// </summary>
		/// <param name="equipmentCode">The string to evaluate.</param>
		/// <returns>A DeviceType enum value.</returns>
		public static DeviceType GetDeviceType( string equipmentCode )
		{
			// TODO: Should a dictionary be used for faster lookups?
			if ( String.IsNullOrEmpty( equipmentCode ) )
			{
				return DeviceType.Unknown;
			}

			DeviceType deviceType = DeviceType.Unknown;

			try
			{
				deviceType = (DeviceType)Enum.Parse( typeof( DeviceType ), equipmentCode, true );
			}
			catch
			{
				deviceType = DeviceType.Other;
			}

			return deviceType;
		}

        protected virtual void DeepCopyTo( Device device )
        {
            device.Language = (Language)this.Language.Clone();

            // Loop through the contained objects calling clone for each one to fill the new lists.
            // Note that we first recreate each of the array list since at this moment, both the
            // source docking station and cloned docking station are both referencing the exact
            // same ArrayLists (because of the MemberwiseClone call)

            device.Options = new List<DeviceOption>( this.Options.Count );
            foreach ( DeviceOption deviceOption in Options )
                device.Options.Add( (DeviceOption)deviceOption.Clone() );
        }


        /// <summary>
        /// All Device subclasses must be cloneable, hence this base class being ICloneable.
        /// This base class's Clone method, is merely abstract as it must exist in order for 
        /// the base class to be ICloneable.
        /// </summary>
        /// <returns></returns>
        public abstract object Clone();

		#endregion  // Methods

	}  // end-class Device


	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define a DeviceOption.
	/// </summary>
	public class DeviceOption : ICloneable
	{

		#region Fields

		private string _code;
        private bool _enabled;

		#endregion

		#region Constructors

        /// <summary>
        /// Creates a new instance of DeviceOption class when device option code is provided.
        /// </summary>
        /// <param name="code">Device option code</param>
        /// <param name="enabled"></param>
        public DeviceOption( string code, bool enabled )
        {
            Code = code;
            Enabled = enabled;
        }

		#endregion 

		#region Properties

		/// <summary>
		/// Gets or sets the device option code.
		/// </summary>
		public string Code
		{
			get
			{
				if ( _code == null ) _code = string.Empty;
				return _code;
			}
			set
			{
                _code = value.Trim().ToUpper();
			}
		}

        /// <summary>
        /// Gets or sets the Enabled flag for the options.
        /// </summary>
        public bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

		#endregion

		#region Methods

		/// <summary>
		///This method returns the string representation of this class.
		/// </summary>
		/// <returns>The string representation of this class</returns>
		public override string ToString()
		{
			return Code;
		}

		/// <summary>
		/// Implementation of ICloneable::Clone - Creates a duplicate of a DeviceOption object.
		/// </summary>
		/// <returns>DeviceOption object</returns>
		public virtual object Clone()
		{
            return this.MemberwiseClone();
		}

		#endregion

	}

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define a DeviceOptionGroup.
	/// </summary>
	public class DeviceOptionGroup : ICloneable
	{

		#region Fields

		private string _code;
		private string _description;
		private DeviceOption[] _options;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new instance of DeviceOptionGroup class.
		/// </summary>
		public DeviceOptionGroup()
		{
			// Do nothing
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the device option group code.
		/// </summary>
		public string Code
		{
			get
			{
				if ( _code == null )
				{
					_code = string.Empty;
				}

				return _code;
			}
			set
			{
				if ( value == null )
				{
					_code = null;
				}
				else
				{
					_code = value.Trim().ToUpper();
				}
			}
		}

		/// <summary>
		/// Gets or sets the device option group description.
		/// </summary>
		public string Description
		{
			get
			{
				if ( _description == null )
				{
					_description = string.Empty;
				}

				return _description;
			}
			set
			{
				if ( value == null )
				{
					_description = null;
				}
				else
				{
					_description = value.Trim();
				}
			}
		}

		/// <summary>
		/// Gets or sets the list of device options contained in the device option group. 
		/// </summary>
		public DeviceOption[] Options
		{
			get
			{
				return _options;	
			}
			set
			{
				_options = value;
			}
		}

		#endregion

		#region Methods

		/// <summary>
		///This method returns the string representation of this class.
		/// </summary>
		/// <returns>The string representation of this class</returns>
		public override string ToString()
		{
			return Code;
		}

		/// <summary>
		/// Implementation of ICloneable::Clone - Creates a duplicate of a DeviceOptionGroup object.
		/// </summary>
		/// <returns>DeviceOptionGroup object</returns>
		public virtual object Clone()
		{
            List<DeviceOption> deviceOptions = new List<DeviceOption>( this.Options.Length );
			foreach ( DeviceOption deviceOption in Options )
				deviceOptions.Add( (DeviceOption)deviceOption.Clone() );

            DeviceOptionGroup deviceOptionGroup = (DeviceOptionGroup)this.MemberwiseClone();
			deviceOptionGroup.Options = (DeviceOption[])deviceOptions.ToArray();

			return deviceOptionGroup;
		}

		#endregion

	}

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Defines device models.
	/// </summary>
	public enum DeviceType 
	{
		Unknown,

        /// <summary>
        /// GasBadge Plus
        /// </summary>
        GBPLS,

        /// <summary>
        /// GasBadge Pro
        /// </summary>
        GBPRO,

		/// <summary>
		/// Tango
		/// </summary>
		TX1,

        /// <summary>
        /// iQuad, Ventis, or Ventis LS.
        /// </summary>
        MX4,

		/// <summary>
		/// Ventis Pro Series (Ventis Pro4 and Ventis Pro5)
		/// </summary>
		VPRO,

        /// <summary>
        /// MX6 iBrid
        /// </summary>
        MX6,

		/// <summary>
		/// SafeCore
		/// </summary>
		SC,

		/// <summary>
		/// Radius BZ1
		/// </summary>
		BZ1,
		
		Other
	}

	/// <summary>
	/// This enum is a direct mapping to the EquipmentSubType enum in the Instrument Driver.  
	/// The docked instrument's sub type is uploaded to iNet as an integer so the integer values
	/// should never be changed.
	/// </summary>
	public enum DeviceSubType
	{
		/// <summary>
		/// This sub-type is used for instrument types that do not have any sub-types, such as Tango and GasBadges.
		/// </summary>
		None = 0,
		/// <summary>
		/// A 4-gas Ventis Pro instrument.
		/// </summary>
		VentisPro4 = 1,
		/// <summary>
		/// A 5-gas Ventis Pro instrument.
		/// </summary>
		VentisPro5 = 2,
		/// <summary>
		/// An MX4 iQuad instrument.
		/// </summary>
		Mx4iQuad = 3,
		/// <summary>
		/// An MX4 Ventis instrument.
		/// </summary>
		Mx4Ventis = 4,
		/// <summary>
		/// An MX4 Scout instrument.
		/// </summary>
		Mx4Scout = 5,
		/// <summary>
		/// An MX4 Ventis-LS instrument.
		/// </summary>
		Mx4VentisLs = 6,
		/// <summary>
		/// Sub-type has not yet been programmed.
		/// </summary>
		Undefined = 0xff
	}

}
