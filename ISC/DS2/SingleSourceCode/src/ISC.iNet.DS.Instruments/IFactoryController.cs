using System;
using System.Collections.Generic;
using System.Text;

namespace ISC.iNet.DS.Instruments
{
	/// <summary>
	/// Interface for all FactoryINSTRUMENT classes.
	/// </summary>
	public interface IFactoryController : IDisposable
	{
		// Even though the FactoryINSTRUMENT classes inherit indirectly from InstrumentController, 
		// its methods will not be visible when the the type is just IFactory.  So expose the methods
		// through a more specific typed property.
		InstrumentController InstrumentController { get; }

		void DisableReplacedInstrument();
	}
}
