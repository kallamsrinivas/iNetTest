using System;
using System.Diagnostics;
using System.Threading;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// Provides functionality to pause operations in between gas operations and data download operations.
    /// </summary>
    public class DataDownloadPauseOperation : DataDownloadPauseAction, IOperation
    {
        public const int DATADOWNLOADPAUSELENGTH = 60; // seconds

        #region Fields

        #endregion Fields

        #region Constructors

        public DataDownloadPauseOperation( DataDownloadPauseAction dataDownloadPauseAction )
            : base( dataDownloadPauseAction )
        {
        }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// </summary>
        /// <returns>Docking station event</returns>
        public DockingStationEvent Execute()
        {
            Stopwatch stopwatch = Log.TimingBegin("DATA DOWNLOAD PAUSE");

            DataDownloadPauseEvent _returnEvent = new DataDownloadPauseEvent(this);
            _returnEvent.DockingStation = Controller.GetDockingStation();
            _returnEvent.DockedInstrument = (ISC.iNet.DS.DomainModel.Instrument)Master.Instance.SwitchService.Instrument.Clone();

            DateTime pauseStartTime = DateTime.UtcNow;
            TimeSpan pauseLength = new TimeSpan(0, 0, DATADOWNLOADPAUSELENGTH);
            TimeSpan elapsedTime = new TimeSpan(0, 0, 0);

            while (elapsedTime < pauseLength)
            {
                Thread.Sleep(500);

                if (!Controller.IsDocked())
                {
                    Log.TimingEnd( "DATA DOWNLOAD PAUSE ***INSTRUMENT UNDOCKED***", stopwatch );
                    throw new InstrumentUndockedDuringPauseException();
                }

                elapsedTime = DateTime.UtcNow - pauseStartTime;
            }

            Log.TimingEnd("DATA DOWNLOAD PAUSE", stopwatch);

            return _returnEvent;
        }

        #endregion Methods
    }

    #region Exceptions


    ////////////////////////////////////////////////////////////////////////////////////////////////////
    ///<summary>
    /// Exception thrown when an instrument is undocked during a pause operation.
    ///</summary>	
    public class InstrumentUndockedDuringPauseException : ApplicationException
    {
        /// <summary>
        /// Creates a new instance of the InstrumentUndockedDuringPauseException class. 
        /// </summary>		
        public InstrumentUndockedDuringPauseException(): base("Instrument undocked during pause")
        {
            // Do Nothing
        }
    }

    #endregion

}
