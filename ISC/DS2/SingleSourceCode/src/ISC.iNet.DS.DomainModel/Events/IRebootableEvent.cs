using System;

using System.Collections.Generic;
using System.Text;

namespace ISC.iNet.DS.DomainModel
{
    ////////////////////////////////////////////////////////////////////////////////////////////////////
    /// <summary>
    /// 
    /// </summary>
    public interface IRebootableEvent
    {
        #region Methods

        /// <summary>
        /// </summary>
        /// <returns>Whether or not the event determined that a reboot is required.</returns>
        bool RebootRequired { get; }

        #endregion

    }
}
