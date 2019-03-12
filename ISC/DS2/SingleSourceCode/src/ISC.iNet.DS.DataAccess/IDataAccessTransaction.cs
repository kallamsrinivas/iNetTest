using ISC.iNet.DS.DomainModel;
using System;
using System.Collections.Generic;
using System.Data;

namespace ISC.iNet.DS.DataAccess
{
    public interface IDataAccessTransaction : IDisposable
    {
        DataAccess.DataSource DataSourceId { get; }

        bool ReadOnly { get; }

        DataAccessHint Hint { get;  }

        DateTime TimestampUtc { get; }

        void Rollback();

        void Commit();

        IList<DockingStationError> Errors { get; }
    }
}