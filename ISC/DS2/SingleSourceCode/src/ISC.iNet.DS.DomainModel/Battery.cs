using System;


namespace ISC.iNet.DS.DomainModel
{
	
	////////////////////////////////////////////////////////////////////////////////////////////////////
	/// <summary>
	/// Provides functionality to define a battery.
	/// </summary>
	public class Battery : Component
	{	
		#region Fields
		#endregion

		#region Constructors

		/// <summary>
		/// Creates a new instance of Battery class.
		/// </summary>
		public Battery()
		{
			Intialize();
		}

		/// <summary>
		/// Creates a new instance of specific Battery class when its serial number is provided.
		/// </summary>
		/// <param name="serialNumber">Serial number of the battery</param>
		public Battery( string serialNumber ) : base ( serialNumber )
		{
			Intialize();
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets or sets the number of minutes battery has been in use.
		/// </summary>
		public int OperationMinutes { get; set; }

		#endregion
		
		#region Methods

		/// <summary>
		/// This method initializes local variables and is called by the constructors of the class.
		/// </summary>
		private void Intialize()
		{
			OperationMinutes = DomainModelConstant.NullInt;
		}

		/// <summary>
		/// Copy the battery's properties to the destination component.
		/// </summary>
		/// <param name="component">The destination component.</param>
		public override void CopyTo( Component component )
		{
			base.CopyTo( component );

            Battery battery = (Battery)component;
			battery.OperationMinutes = OperationMinutes;
		}

        /// <summary>
        /// Returns whether or no this battery is a rechargeable type or not.
        /// </summary>
        /// <returns></returns>
        public bool IsRechargeable()
        {
            return BatteryCode.IsRechargable( this.Type.Code );
        }

		#endregion
	}

    /// <summary>
    /// Battery codes for battery packs that are commonly referred to within source code.
    /// </summary>
    public class BatteryCode
    {
        /// <summary>
        /// 9v Disposable Battery (alkaline)  (used in T82)
        /// </summary>
        public const string Disposable9V = "BP001";
 
        /// <summary>
        /// 4.1v Lithium Battery Pack (rechargeable) (used in iTX and VX500)
        /// </summary>
        public const string LegacyLithium41V   = "BP002";

        /// <summary>
        /// 4.2v Lithium Battery Pack (rechargeable) (used in iTX and VX500)
        /// </summary>
        public const string LegacyLithium42V = "BP003";

        /// <summary>
        /// 4.5v Alkaline Battery Pack (3-AA's) (wused in iTX and VX500)
        /// </summary>
        public const string LegacyAlkaline45V  = "BP004";

        /// <summary>
        /// Alkaline battery pack for MX6.
        /// </summary>
        public const string MX6Alkaline     = "BP005";

        /// <summary>
        /// 2-cell lithium battery pack for MX6.
        /// </summary>
        public const string MX6Lithium2Cell = "BP006";

        /// <summary>
        /// 3-cell lithium battery pack for MX6.
        /// </summary>
        public const string MX6Lithium3Cell = "BP007";

        /// <summary>
        /// Alkaline battery pack for MX4.
        /// </summary>
        public const string MX4Alkaline = "BP008";

        // SGF  Sep-22-2008  DSZ-1692
        // Expanding number of lithium batteries for the MX4 to 4 (expected to be 2 for diffusion and 2 for pump).
        // Do not have specifics for any of these batteries, so cannot provide better constant names than what is shown below.
        // When specifics are known, the constant names could be changed to something more meaningful.

        /// <summary>
        /// Lithium polymer pack for MX4 - Alternative 1
        /// </summary>
        public const string MX4Lithium1 = "BP009";

        /// <summary>
        /// Lithium polymer pack for MX4 - Alternative 2
        /// </summary>
        public const string MX4Lithium2 = "BP010";

        /// <summary>
        /// Lithium polymer pack for MX4 - Alternative 3
        /// </summary>
        public const string MX4Lithium3 = "BP011";

        /// <summary>
        ///  Lithium polymer pack for MX4 - Alternative 4
        /// </summary>
        public const string MX4Lithium4 = "BP012";

        /// <summary>
        /// Dual-cell lithium polymer pack for MX4 - Alternative 5
        /// </summary>
        public const string MX4Lithium5 = "BP013";
 
        /// <summary>
        /// Dual-cell lithium polymer pack for MX4 - Alternative 6
        /// </summary>
        public const string MX4Lithium6 = "BP014";

        /// <summary>
        /// Dual-cell lithium polymer pack for MX4 - Alternative 7
        /// </summary>
        public const string MX4Lithium7 = "BP015"; 
        /// <summary>
        /// Dual-cell lithium polymer pack for MX4 - Alternative 8
        /// </summary>
        public const string MX4Lithium8 = "BP016";

        /// <summary>
        /// Private ctor - can't instantate; this class is static attributes only.
        /// </summary>
        private BatteryCode() {}

        /// <summary>
        /// </summary>
        /// <param name="code">A battery code.  e.g. "BP0001".</param>
        /// <returns>Returns whether or not a battery of the specified type is rechargeable or not.</returns>
        static public bool IsRechargable( string code )
        {
            switch ( code )
            {
                case DomainModel.BatteryCode.MX6Lithium2Cell:
                case DomainModel.BatteryCode.MX6Lithium3Cell:
                case DomainModel.BatteryCode.MX4Lithium1:
                case DomainModel.BatteryCode.MX4Lithium2:
                case DomainModel.BatteryCode.MX4Lithium3:
                case DomainModel.BatteryCode.MX4Lithium4:
                case DomainModel.BatteryCode.MX4Lithium5:
                case DomainModel.BatteryCode.MX4Lithium6:
                case DomainModel.BatteryCode.MX4Lithium7:
                case DomainModel.BatteryCode.MX4Lithium8:
                    return true;
                default:
                    return false;
            }
        }

    }  // end-class

}  // end-namespace
