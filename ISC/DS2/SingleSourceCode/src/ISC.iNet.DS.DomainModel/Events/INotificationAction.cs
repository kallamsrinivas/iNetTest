using System;

using System.Collections.Generic;
using System.Text;

namespace ISC.iNet.DS.DomainModel
{
    /// <summary>
    /// Tag interface to denote DockingStationActions for which iNet should
    /// be notified about.
    /// iNet needs to be notified of certain 'actions' if and when the VDS decides 
    /// to do do them, for example, failed Leak check, Unvailable Gas, etc.
    /// </summary>
    public interface INotificationAction
    {

        // Currently, there's no need for any methods.
        // We're merely using this as a tag interface.
    }
}
