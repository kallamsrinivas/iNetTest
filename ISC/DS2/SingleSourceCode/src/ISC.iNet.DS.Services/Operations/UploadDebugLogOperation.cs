using System;
using System.Collections.Generic;
using System.Diagnostics;
using ISC.iNet.DS.DomainModel;
using System.Text;
using ISC.WinCE.Logger;


namespace ISC.iNet.DS.Services
{
    /// <summary>
    /// </summary>
    public class UploadDebugLogOperation : UploadDebugLogAction, IOperation
    {
        #region Constructors

        /// <summary>
        /// </summary>
        public UploadDebugLogOperation() {}

        public UploadDebugLogOperation( UploadDebugLogAction uploadDebugLogAction ) : base( uploadDebugLogAction ) { }

        #endregion Constructors

        #region Methods

        /// <summary>
        /// </summary>
        /// <returns>Docking station event</returns>
        public DockingStationEvent Execute()
        {
            StringBuilder sb = new StringBuilder();

            Queue<string> logMessages = Log.GetMessages();

            while ( logMessages.Count > 0 )
                sb.Append( logMessages.Dequeue() );

            UploadDebugLogEvent uploadDebugLogEvent = new UploadDebugLogEvent( this );

            uploadDebugLogEvent.LogText = sb.ToString();
            
            Log.Debug( string.Format( "{0}: LogText.Length={1}", Name, uploadDebugLogEvent.LogText.Length ) );

            return uploadDebugLogEvent;
        }

        #endregion Methods
    }
}
