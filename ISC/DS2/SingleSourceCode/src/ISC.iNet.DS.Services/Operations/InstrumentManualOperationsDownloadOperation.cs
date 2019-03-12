using System;
using System.Collections.Generic;
using System.Diagnostics;
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.WinCE.Logger;



namespace ISC.iNet.DS.Services
{
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Provides functionality to download logged manual operations data from the instrument.
    /// </summary>
    public class InstrumentManualOperationsDownloadOperation : InstrumentManualOperationsDownloadAction, IOperation
    {
        #region Constructors

        public InstrumentManualOperationsDownloadOperation( InstrumentManualOperationsDownloadAction instrumentManualOperationsDownloadAction )
            : base( instrumentManualOperationsDownloadAction )
        {
        }

        #endregion

        #region Methods

        /// <summary>
        /// Executes an instrument read settings operation.
        /// </summary>
        /// <returns>Docking station event</returns>
        public DockingStationEvent Execute()
        {
            Stopwatch stopwatch = Log.TimingBegin("MANUAL OPERATIONS DOWNLOAD");

            InstrumentManualOperationsDownloadEvent downloadEvent = new InstrumentManualOperationsDownloadEvent( this );
            downloadEvent.DockedInstrument = (ISC.iNet.DS.DomainModel.Instrument)Master.Instance.SwitchService.Instrument.Clone();
            downloadEvent.DockingStation = Master.Instance.ControllerWrapper.GetDockingStation();

            using ( InstrumentController instrumentController = Master.Instance.SwitchService.InstrumentController )
            {
                instrumentController.Initialize( InstrumentController.Mode.Batch );

                Log.Debug( "MANUAL GAS OPERATIONS: Downloading" );

                // INS-3145 - Due to a bug in GBPro, a manual gas operation is sometimes logged 
                // with an invalid sensor resolution (decimal places = 255).  When this happens,
                // the instrument driver will throw an ArgumentOutOfRangeException.  If we
                // catch one, we need to tell iNet.  We don't rethrow the exception, to prevent
                // the docking station from going "unavailable" and instead just treat it
                // as if the log was empty.  This allows the corrupt log to be cleared.
                try
                {
                    downloadEvent.GasResponses = new List<SensorGasResponse>( instrumentController.GetManualGasOperations() );
                }
                catch ( ArgumentOutOfRangeException aoore )
                {
                    Log.Error( aoore );
                    downloadEvent.Errors.Add( new DockingStationError( "Corrupt manual gas operations log encountered.", DockingStationErrorLevel.Warning, downloadEvent.DockedInstrument.SerialNumber ) );
                }

                Log.Debug( "MANUAL GAS OPERATIONS: " + downloadEvent.GasResponses.Count + " downloaded." );

            } // end-using

            Log.TimingEnd("MANUAL OPERATIONS DOWNLOAD",stopwatch);

            return downloadEvent;  // Return the populated event.
        }

        #endregion

    } // end-class
}