using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using ISC.iNet.DS.DomainModel;
using ISC.WinCE.Logger;

namespace ISC.iNet.DS.Services
{
    public class RebootOperation : RebootAction, IOperation
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="rebootAction"></param>
        public RebootOperation( RebootAction rebootAction ) : base( rebootAction ) {}

        /// <summary>
        /// 
        /// </summary>
        /// <returns>null.</returns>
        public DockingStationEvent Execute()
        {
            Log.Warning( string.Format( "{0} invoking PrepareForReset & PerformSoftReset", Name ) );

            Master.Instance.PrepareForReset();

            Controller.PerformSoftReset();

            return null;
        }
    }
}
