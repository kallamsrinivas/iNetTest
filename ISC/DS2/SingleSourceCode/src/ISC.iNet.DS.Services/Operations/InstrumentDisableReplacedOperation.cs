using System;
using System.Collections.Generic;
using System.Text;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.WinCE.Logger;

namespace ISC.iNet.DS.Services
{
	public class InstrumentDisableReplacedOperation : InstrumentDisableReplacedAction, IOperation
	{
		#region Constructors

		public InstrumentDisableReplacedOperation() { }

		public InstrumentDisableReplacedOperation( InstrumentDisableReplacedAction instrumentReplacedAction )
			: base( instrumentReplacedAction )
		{

		}
				 
		#endregion

		#region Methods

		public DockingStationEvent Execute()
		{
			string funcMsg = Name + ".Execute";
			Log.Debug( funcMsg );

			// create return event
			InstrumentDisableReplacedEvent returnEvent = new InstrumentDisableReplacedEvent( this );
			returnEvent.DetectedReplacedSerialNumber = this.ReplacedSerialNumber;

			try
			{
				// record docking station and docked instrument on return event, 
				// instrument serial number will be stored in the event journal
				returnEvent.DockingStation = Configuration.DockingStation;
				returnEvent.DockedInstrument = (ISC.iNet.DS.DomainModel.Instrument)Master.Instance.SwitchService.Instrument.Clone();

				// check that the instrument is still docked
				if ( !Controller.IsDocked() )
				{
					throw new InstrumentUndockedDuringDisableReplacedException();
				}

				// establish communication with the docked instrument, verify it has the expected
				// serial number, and then disable it
				using ( IFactoryController factoryController = SwitchService.CreateFactoryInstrumentController() )
				{
					factoryController.InstrumentController.Initialize( InstrumentController.Mode.Batch );

					returnEvent.SerialNumberBefore = factoryController.InstrumentController.GetSerialNumber();

					// ensure the docked instrument serial number, the instrument serial number that will be saved to the event journal, and the detected 
					// replaced instrument serial number all match before continuing
					if ( returnEvent.SerialNumberBefore == this.ReplacedSerialNumber && returnEvent.DockedInstrument.SerialNumber == this.ReplacedSerialNumber )
					{
						Log.Warning( string.Format( "DISABLING REPLACED INSTRUMENT: {0}", returnEvent.SerialNumberBefore ) );
						factoryController.DisableReplacedInstrument();
						Log.Warning( string.Format( "INSTRUMENT DISABLED" ) );

						// set flag on switch service to stop anything else from happening until the disabled instrument is undocked
						Master.Instance.SwitchService.IsInstrumentReplaced = true;

						returnEvent.Passed = true;
						returnEvent.SerialNumberAfter = factoryController.InstrumentController.GetSerialNumber();
					}
				}
			}
			catch ( InstrumentUndockedDuringDisableReplacedException )
			{
				throw;
			}
			catch ( Exception ex )
			{
				Log.Error( string.Format( "{0} - Caught Exception", funcMsg ), ex );

				// check that the instrument is still docked
				if ( !Master.Instance.SwitchService.IsDocked() )
				{
					throw new InstrumentUndockedDuringDisableReplacedException();
				}
			}
			finally
			{
				// log operation results
				Log.Debug( string.Format( "WAS INSTRUMENT DISABLED: {0} - Detected Replaced S/N: {1} - S/N (before): {2} - S/N (after): {3}", returnEvent.Passed, returnEvent.DetectedReplacedSerialNumber, returnEvent.SerialNumberBefore, returnEvent.SerialNumberAfter ) );
			}

			return returnEvent;
		}

		#endregion
	}

	#region Exceptions

	////////////////////////////////////////////////////////////////////////////////////////////////////
	///<summary>
	/// Exception thrown when an instrument is undocked during a instrument disable replaced operation.
	/// Used to help display the return instrument to ISC message even if the replaced instrument is 
	/// undocked early by the user.
	///</summary>	
	public class InstrumentUndockedDuringDisableReplacedException : ApplicationException
	{
		/// <summary>
		/// Creates a new instance of the InstrumentUndockedDuringDisableReplacedException class. 
		/// </summary>		
		public InstrumentUndockedDuringDisableReplacedException()
			: base( "Instrument undocked during disable replaced" )
		{
			// Do Nothing
		}
	}

	#endregion
}
