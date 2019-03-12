using System;
using System.Text;
using System.Diagnostics;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;


namespace ISC.iNet.DS.Services		//IDS.Operation
{
	/// <summary>
	/// Summary description for PingOperation.
	/// </summary>
	public class PingOperation : NothingAction , IOperation
	{
		#region Fields
				
		#endregion
		
		#region Constructors

		/// <summary>
		/// Creates a new instance of DiscoveryOperation class.
		/// </summary>
		public PingOperation()
		{			
		}

		#endregion

		#region Methods
			
		/// <summary>
		/// Executes an instrument discovery operation.
		/// </summary>
		/// <returns>Docking station event</returns>
		public DockingStationEvent Execute()
		{
            InstrumentNothingEvent instrumentNothingEvent;

            using ( InstrumentController instrumentController = SwitchService.CreateInstrumentController() )
            {
                // Create the return event.
                instrumentNothingEvent = new InstrumentNothingEvent( this );

                // Open the serial port connection needed to communicate with the instrument.
                instrumentController.Initialize();
            }

			return instrumentNothingEvent;
		}

		#endregion

	}

}
