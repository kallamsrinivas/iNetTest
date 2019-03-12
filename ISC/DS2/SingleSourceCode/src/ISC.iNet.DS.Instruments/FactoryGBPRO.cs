using System;
using System.Collections.Generic;
using System.Text;
using ISC.Instrument.Driver;
using ISC.Instrument.TypeDefinition;

namespace ISC.iNet.DS.Instruments
{
	public class FactoryGBPRO : GBPRO, IFactoryController
	{
		#region Constructors

		/// <summary>
		/// Instrument controller class for GasBadge Pro instruments with access to factory methods.
		/// </summary>
		public FactoryGBPRO()
			: base( new GbProFactoryDriver() )
		{

		}

		#endregion

		#region Properties

		/// <summary>
		/// Provides local access to strongly typed factory driver.
		/// </summary>
		private GbProFactoryDriver FactoryDriver
		{
			get
			{
				return (GbProFactoryDriver)this.Driver;
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
			FactoryDriver.setOperatingMode(OperatingMode.FactoryBirth);
		}

		#endregion
	}
}
