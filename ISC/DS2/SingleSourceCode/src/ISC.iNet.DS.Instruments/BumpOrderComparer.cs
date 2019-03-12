using System.Collections.Generic;
using ISC.iNet.DS.DomainModel;


namespace ISC.iNet.DS.Instruments
{
    /// <summary>
    /// Provides functionality to sort sensors by bump order.
    /// Used by method InstrumentController.SortSensorsByBumpOrder.
    /// </summary>
    public class BumpOrderComparer : IComparer<InstalledComponent>
    {
        #region Methods

        /// <summary>
        /// Implementation of the IComparer.Compare method that sorts sensors by bump order.
        /// </summary>
        /// <param name="instComp1"></param>
        /// <param name="instComp2"></param>
        /// <returns></returns>
        public int Compare( InstalledComponent instComp1, InstalledComponent instComp2 )
        {
            if ( instComp1 == instComp2 )
                return 0;

            if ( !( instComp1.Component is Sensor ) )
                return 1;

            if ( !( instComp2.Component is Sensor ) )
                return -1;

            Sensor sensor1 = (Sensor)instComp1.Component;
            Sensor sensor2 = (Sensor)instComp2.Component;

            if ( sensor1.CalibrationGas.BumpOrder > sensor2.CalibrationGas.BumpOrder )
                return 1;

            if ( sensor1.CalibrationGas.BumpOrder < sensor2.CalibrationGas.BumpOrder )
                return -1;

            return 0;
        }

        #endregion
    }

}
