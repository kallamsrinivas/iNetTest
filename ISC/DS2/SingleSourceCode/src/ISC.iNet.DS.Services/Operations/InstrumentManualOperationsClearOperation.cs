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
    /// Provides functionality to clear logged manual operations data from the instrument.
    /// </summary>
    public class InstrumentManualOperationsClearOperation : InstrumentManualOperationsClearAction, IOperation
    {
        #region Fields

        #endregion Fields

        #region Constructors

        public InstrumentManualOperationsClearOperation( InstrumentManualOperationsClearAction instrumentManualOperationsClearAction )
            : base( instrumentManualOperationsClearAction )
        {
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// </summary>
        /// <returns>Docking station event</returns>
        public DockingStationEvent Execute()
        {
            Stopwatch stopwatch = Log.TimingBegin("MANUAL OPERATIONS CLEAR");

            InstrumentManualOperationsClearEvent clearEvent = new InstrumentManualOperationsClearEvent( this );
            clearEvent.DockedInstrument = (ISC.iNet.DS.DomainModel.Instrument)Master.Instance.SwitchService.Instrument.Clone();
            clearEvent.DockingStation = Master.Instance.ControllerWrapper.GetDockingStation();

            using ( InstrumentController instrumentController = Master.Instance.SwitchService.InstrumentController )
            {
                instrumentController.Initialize();

                instrumentController.ClearManualGasOperations();

                Log.Debug( Name + ": Manual gas operations cleared.");

            } // end-using

            Log.TimingEnd("MANUAL OPERATIONS CLEAR",stopwatch);

            return clearEvent;
        }

        #endregion Methods
    }
}
