using ISC.Instrument.Driver;

namespace ISC.iNet.DS.Instruments
{
	public class FactorySC : SC, IFactoryController
	{
		#region Constructors

		/// <summary>
		/// Instrument controller class for SafeCore modules with access to factory methods.
		/// </summary>
		public FactorySC()
			: base( new SafeCoreFactoryDriver() )
		{

		}

		#endregion

		#region Properties

		/// <summary>
		/// Provides local access to strongly typed factory driver.
		/// </summary>
		private SafeCoreFactoryDriver FactoryDriver
		{
			get
			{
				return (SafeCoreFactoryDriver)this.Driver;
			}
		}

		/// <summary>
		/// Provides access to non-factory methods.
		/// </summary>
		public InstrumentController InstrumentController
		{
			get
			{
				return this;
			}
		}

		#endregion

		#region Methods

		/// <summary>
		/// Used to disable an instrument that iNet has marked as already being replaced,
		/// and should be returned to ISC.
		/// </summary>
		public void DisableReplacedInstrument()
		{
			FactoryDriver.enableReturnToIsc(true);
		}

		#endregion
	}
}