using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using ISC.iNet.DS.DomainModel;



namespace ISC.iNet.DS.DataAccess
{
    public class ScheduledHourlyDataAccess : ScheduledDaysDataAccess
    {
        protected internal override Schedule CreateFromReader( IDataReader reader, DataAccessOrdinals ordinals )
        {
            bool[] days = GetDaysFromReader( reader, ordinals );

            return new ScheduledHourly(
                GetId( reader, ordinals ),
                GetRefId( reader, ordinals ),
                GetName( reader, ordinals ),
                GetEventCode( reader, ordinals ),
				GetEquipmentCode( reader, ordinals ),
                GetEquipmentSubTypeCode( reader, ordinals ),
                GetEnabled( reader, ordinals ),
                GetOnDocked( reader, ordinals ),
                GetInterval( reader, ordinals ),
                GetStartDate( reader, ordinals ),
                GetRunAtTime( reader, ordinals ),
                days );
        }
    }
}
