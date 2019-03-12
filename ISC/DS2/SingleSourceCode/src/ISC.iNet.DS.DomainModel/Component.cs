using System;
using System.Collections.Generic;
using System.Reflection;

namespace ISC.iNet.DS.DomainModel
{
	
	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides base functionality for classes defining a component.
	/// </summary>
	public class Component : ICloneable
	{
		
		#region Fields

        public DateTime SetupDate { get; set; }

		protected string _serialNumber = string.Empty;
		
        private string _softwareVersion = string.Empty;
		private string _partNumber = string.Empty;
        protected ComponentType _type = new ComponentType();
		private string _manufacturerCode = string.Empty;

        private string _setupVersion = string.Empty;
        private string _setupTech = string.Empty;
        private string _hardwareVersion = string.Empty;

        /// <summary>
        /// Indicates if user wishes this component be enabled or disabled
        /// while installed in the instrument.
        /// </summary>
        public virtual bool Enabled { get; set; }

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new instance of Component class.
		/// </summary>
		public Component()
		{
            Init();
		}

		/// <summary>
		/// Creates a new instance of Component class when its serial number is provided.
		/// </summary>
        /// <param name="uid">
        /// The component's UID. The UID is the serial number and sensor
        /// code separated by a "#".  e.g. "1234567890#S0020".
        /// </param>
		public Component( string uid )
		{
            Init();
			Uid = uid;
		}

        private void Init()
        {
            SetupDate = DomainModelConstant.NullDateTime;
            Enabled = true;
        }

		#endregion

		#region Properties

        /// <summary>
        /// Gets or sets the component UID.  The UID is the serial number and sensor
        /// code separated by a "#".  e.g. "1234567890#S0020".
        /// </summary>
        public virtual string Uid
		{
            get
            {
                if ( SerialNumber == string.Empty )
                    return SerialNumber;

                return SerialNumber + '#' + Type;
            }
			set
			{
				if ( value == null )
				{
					_serialNumber = null;
					_type = null;
                    return;
				}

                int hash = value.LastIndexOf( '#' );

                if ( hash == -1 )
                {
                    _serialNumber = value.Trim().ToUpper();
                    _type = null;
                }
                else
                {
                    _serialNumber = value.Substring( 0, hash  ).Trim().ToUpper();
                    string typeString = value.Substring( hash + 1 );
                    _type = new ComponentType( typeString.Trim().ToUpper() );
                }
			}		
		}

        /// <summary>
        /// Returns the base serial number. i.e., the simple serial number, minus the sensor code.
        /// </summary>
        public string SerialNumber
        {
            get
            {
                if ( _serialNumber == null ) _serialNumber = string.Empty;
                return _serialNumber;
            }
        }

		/// <summary>
		/// Gets or sets the component part number.
		/// </summary> 
        public string PartNumber
		{
			get { return _partNumber; }
			set { _partNumber = ( value == null ) ? string.Empty : value.Trim().ToUpper(); }
		}

		/// <summary>
		/// Gets the component type.
		/// </summary>
		public virtual ComponentType Type
		{
			get { return _type; }
            set { _type = ( value == null ) ? new ComponentType() : value; }
		}

		/// <summary>
		/// Gets or sets the device software version.
		/// </summary>
		public string SoftwareVersion
		{
			get { return _softwareVersion; }
			set { _softwareVersion = ( value == null ) ? string.Empty : value; }
		}

		/// <summary>
		/// Gets or sets the component manufacturer.
		/// </summary>
		public string ManufacturerCode
		{
            get { return _manufacturerCode; }
			set { _manufacturerCode = ( value == null ) ? string.Empty : value; }
		}

        /// <summary>
        /// Gets or sets the sensor setup technician.
        /// </summary>
        public string SetupTech 
        {
            get { return _setupTech; }
            set { _setupTech = ( value == null ) ? string.Empty : value; }
        }

        /// <summary>
        /// Gets or Sets the version number used by setup software to
        /// configure sensor
        /// </summary>
        public string SetupVersion
        {
            get { return _setupVersion; }
            set { _setupVersion = ( value == null ) ? string.Empty : value; }
        }

        /// <summary>
        /// Gets or Sets the hardware version number
        /// </summary>
        public string HardwareVersion
        {
            get { return _hardwareVersion; }
            set { _hardwareVersion = ( value == null ) ? string.Empty : value; }

        }

		#endregion  Properties

		#region Methods

        /// <summary>
        /// Returns true if the passed-in UID string starts with "VIRTUAL".
        /// </summary>
        /// <param name="uid"></param>
        /// <returns></returns>
        internal static bool IsVirtualUid( string uid )
        {
            return uid.StartsWith( "VIRTUAL" );
        }

		/// <summary>
		///This method returns the string representation of this class.
		/// </summary>
		/// <returns>The string representation of this class</returns>
		public override string ToString()
		{
			return Uid;
		}

		/// <summary>
		/// Copy all of the properties to the destination object.
		/// </summary>
		/// <param name="component">The destination object.</param>
		public virtual void CopyTo( Component component )
		{
            component.ManufacturerCode = ManufacturerCode;
			component.PartNumber = PartNumber;			
			component.Uid = Uid;
            component.SetupVersion = SetupVersion;
            component.SetupTech = SetupTech;
            component.SetupDate = SetupDate;
            component.HardwareVersion = HardwareVersion;
			component._type = (ComponentType)Type.Clone();
            component.Enabled = Enabled;
            component.SoftwareVersion = SoftwareVersion;
		}

		/// <summary>
		/// Implementation of ICloneable::Clone - Creates a duplicate of an component object.
		/// </summary>
		/// <returns>component object</returns>
		public virtual object Clone()
		{
            // 'this' will be a subclass of component, i.e. battery or sensor.
            // Get the default constructor for whatever 'this' is, and call it.  
            // (We can't just say "new Component()", otherwise we'd just get 
            // a Component object and not a sensor or battery)
            ConstructorInfo ctorInfo = this.GetType().GetConstructor( new Type[0] );

            Component component = (Component)ctorInfo.Invoke( null );

			CopyTo( component );

			return component;
		}

		#endregion

	}

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define a Component Type.
	/// </summary>
	public class ComponentType : ICloneable
	{

		#region Fields

		private string _code;
		private DeviceType _type;
		private string _description;

		#endregion

		#region Constructors
		
		/// <summary>
		/// Creates a new instance of ComponentType class.
		/// </summary>
		public ComponentType()
		{
			// Do nothing
		}

		/// <summary>
		/// Creates a new instance of ComponentType class when its code is provided. 
		/// </summary>
		public ComponentType( string code )
		{
			Code = code;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the component type code.
		/// </summary>
		public virtual string Code
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
		/// Gets or sets the component type description.
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
		/// Gets or sets the device type.
		/// </summary>
		public DeviceType DeviceType
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

		#endregion

		#region Methods

		/// <summary>
		/// This override returns the Code.
		/// </summary>
        /// <returns>This override returns the Code.</returns>
		public override string ToString()
		{
			return Code;
		}

		/// <summary>
		/// Implementation of ICloneable::Clone - Creates a duplicate of a ComponentType object.
		/// </summary>
		/// <returns>ComponentType object</returns>
		public virtual object Clone()
		{
            return this.MemberwiseClone();
		}

		#endregion

	}

	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to describe an installed component.
	/// </summary>
	public class InstalledComponent : ICloneable
	{
		
		#region Fields

        private const int MAXIMUM_ALLOWED_POSITIONS = 20;
        private const int UNINSTALLED_POSITION = 0;
		private Component _component;
		private int _position;

		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new instance of InstalledComponent class.
		/// </summary>
		public InstalledComponent()
		{
			// Do nothing
		}

		#endregion

		#region Properties

        /// <summary>
        /// Returns the maximum allowed position for any component.  No
        /// instrument will have this many positions.  Any component whose
        /// position equals this value doesn't really have a position. i.e.
        /// the position is non-applicable for the component type; e.g.
        /// batteries typically have a position equal to MaxAllowedPosition.
        /// </summary>
        static public int MaxAllowedPosition
        {
            get
            {
                return MAXIMUM_ALLOWED_POSITIONS;
            }
        }

        /// <summary>
        /// Returns the position a component should have when it's uninstalled.
        /// </summary>
        static public int UninstalledPosition
        {
            get
            {
                return UNINSTALLED_POSITION;
            }
        }

		/// <summary>
		/// Gets or sets position of the component.
		/// </summary>
		public int Position
		{
			get
			{
				return _position;
			}
			set
			{
				_position = value;
			}
		}

		/// <summary>
		/// Gets or sets the component.
		/// </summary>
		public Component Component
		{
			get
			{
				if ( _component == null )
				{
					_component = new Component();
				}

				return _component;
			}
			set
			{
				_component = value;
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
			return Position.ToString();
		}

		/// <summary>
		/// Implementation of ICloneable::Clone - Creates a duplicate of an InstalledComponent object.
		/// </summary>
		/// <returns>InstalledComponent object</returns>
		public virtual object Clone()
		{
            InstalledComponent installedComponent = (InstalledComponent)this.MemberwiseClone();
			
			installedComponent.Component = (Component) Component.Clone();
			
			return installedComponent;
		}

		#endregion

	}
}
