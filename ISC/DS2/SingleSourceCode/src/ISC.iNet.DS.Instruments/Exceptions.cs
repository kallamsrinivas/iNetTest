using System;


namespace ISC.iNet.DS.Instruments
{
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    ///<summary>
    /// Exception thrown when we are unable to ping an instrument.
    ///</summary>	
    public class InstrumentPingFailedException : ApplicationException
    {
        const string _defaultMsg = "Unable to ping the instrument.";

        /// <summary>
        /// Intializes an instance of the InstrumentPingFailedException class. 
        /// </summary>		
        public InstrumentPingFailedException() : base( _defaultMsg ) { }

        /// <summary>
        /// Intializes an instance of the InstrumentPingFailedException class. 
        /// </summary>		
        public InstrumentPingFailedException( string msg ) : base( _defaultMsg + " " + msg ) { }

        /// <summary>
        /// Intializes an instance of the InstrumentPingFailedException class by reporting the source of the error.
        /// </summary>
        ///<param name="e">Source</param>
        public InstrumentPingFailedException( Exception e ) : base( _defaultMsg, e ) { }
    }

    /// <summary>
    /// Exception thrown when a sensor is found to be in error mode.
    /// </summary>
    public class SensorErrorModeException : ApplicationException
    {
        /// <summary>
        /// Create an instance of the exception.
        /// </summary>
        public SensorErrorModeException() : base( "A sensor is in error mode." ) { }

        /// <summary>
        /// Create an instance of the exception using the specified error message.
        /// </summary>
        public SensorErrorModeException( string msg ) : base( msg ) { }

        /// <summary>
        /// Create an instance of the exception that wraps another exception.
        /// </summary>
        /// <param name="e">The exception to wrap.</param>
        public SensorErrorModeException( Exception e ) : base( "A sensor is in error mode.", e ) { }
    }

    ///<summary>
    /// Exception thrown when an instrument operation is attempted where no instrument is docked.
    ///</summary>	
    public class InstrumentNotDockedException : ApplicationException
    {
        public InstrumentNotDockedException( string msg ) : base( msg ) {}

        /// <summary>
        /// Creates a new instance of the InstrumentNotDockedException class. 
        /// </summary>		
        public InstrumentNotDockedException()
            : base( "Requested operation cannot be performed because an instrument is not docked!" )
        {
            // Do Nothing
        }

        /// <summary>
        /// Creates a new instance of the InstrumentNotDockedException class by reporting the source of the error.
        /// </summary>
        ///<param name="e">Source</param>
        public InstrumentNotDockedException( Exception e )
            : base( "Requested operation cannot be performed because an instrument is not docked!", e )
        {
            // Do Nothing
        }

    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////
    ///<summary>
    /// Exception thrown when the class constructor receives a null value.
    ///</summary>	
    ///<remarks> // INS-7657 RHP v7.5.2 Display Instrument Not Ready Message to be specific that the error is due to Sesnor not biased within 2 hours</remarks>
    public class InstrumentNotReadyException : ApplicationException
    {

        /// <summary>
        /// Creates a new instance of the InstrumentNotReadyException class. 
        /// </summary>		
        public InstrumentNotReadyException() : base("Requested operation cannot be performed because sensor is not biased!") { }

        /// <summary>
        /// Create an instance of the exception using the specified error message.
        /// </summary>
        public InstrumentNotReadyException(string msg) : base(msg) { }

        /// <summary>
        /// Creates a new instance of the InstrumentNotReadyException class by reporting the source of the error.
        /// </summary>
        ///<param name="e">Source</param>
        public InstrumentNotReadyException(Exception e) : base("Requested operation cannot be performed because sensor is not biased!", e) { }
    }

}
