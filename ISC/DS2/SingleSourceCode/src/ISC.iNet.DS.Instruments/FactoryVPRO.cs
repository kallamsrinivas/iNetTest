using System;
using System.Collections.Generic;
using System.Text;
using ISC.Instrument.Driver;
using ISC.Instrument.TypeDefinition;

namespace ISC.iNet.DS.Instruments
{
	public class FactoryVPRO : VPRO, IFactoryController
	{
		#region Constructors

		/// <summary>
		/// Instrument controller class for Ventis Pro Series instruments with access to factory methods.
		/// </summary>
		public FactoryVPRO()
			: base( new VentisProFactoryDriver() )
		{

		}

		#endregion

		#region Properties

		/// <summary>
		/// Provides local access to strongly typed factory driver.
		/// </summary>
		private VentisProFactoryDriver FactoryDriver
		{
			get
			{
				return (VentisProFactoryDriver)this.Driver;
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
			FactoryDriver.enableReturnToIsc( true );
		}

		#endregion
	}
}