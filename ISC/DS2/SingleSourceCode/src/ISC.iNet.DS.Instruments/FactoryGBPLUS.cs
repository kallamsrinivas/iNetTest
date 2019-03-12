using System;
using System.Collections.Generic;
using System.Text;
using ISC.Instrument.Driver;
using ISC.Instrument.TypeDefinition;

namespace ISC.iNet.DS.Instruments
{
	public class FactoryGBPLUS : GBPLUS, IFactoryController
	{
		#region Constructors

		/// <summary>
		/// Instrument controller class for GasBadge Plus instruments with access to factory methods.
		/// </summary>
		public FactoryGBPLUS()
			: base( new GbPlusFactoryDriver() )
		{

		}

		#endregion

		#region Properties

		/// <summary>
		/// Provides local access to strongly typed factory driver.
		/// </summary>
		private GbPlusFactoryDriver FactoryDriver
		{
			get
			{
				return (GbPlusFactoryDriver)this.Driver;
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
			// don't disable the GB Plus, just return
			return;
		}

		#endregion
	}
}
