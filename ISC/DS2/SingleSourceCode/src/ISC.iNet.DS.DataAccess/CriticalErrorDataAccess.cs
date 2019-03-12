using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Data.Common; // DataTableMapping
using System.Data.SQLite;
using ISC.iNet.DS.DomainModel;

namespace ISC.iNet.DS.DataAccess
{
    /// <summary>
    /// Data access for CriticalError table.
    /// </summary>
    public class CriticalErrorDataAccess : DataAccess
    {
        public CriticalErrorDataAccess() { }

        #region public methods

        /// <summary>
        /// Finds and returns all Critical Instrument Errors in the database.
        /// </summary>
        /// <returns></returns>
        public virtual List<CriticalError> FindAll()
        {
            using ( DataAccessTransaction trx = new DataAccessTransaction( true ) )
            {
                return FindAll( trx );
            }
        }

        /// <summary>
        /// Finds and returns all critical errors in the database.
        /// </summary>
        public List<CriticalError> FindAll(DataAccessTransaction trx)
        {
            List<CriticalError> list = new List<CriticalError>();

            using (IDbCommand cmd = GetCommand("SELECT * FROM " + TableName, trx))
            {
                using ( IDataReader reader = cmd.ExecuteReader() )
                {
                    DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );

                    while ( reader.Read() )
                        list.Add( CreateFromReader( reader, ordinals ) );
                }
            }

            return list;
        }

        /// <summary>
        /// Delete the critical instrument errors from the database
        /// </summary>
        /// <returns></returns>
        public bool Delete()
        {
            using (DataAccessTransaction trx = new DataAccessTransaction())
            {
                Delete(trx);
                return true;
            }
        }

                /// <summary>
        /// Delete the critical instrument errors
        /// </summary>
        /// <param name="trx">The Transcation</param>
        /// <returns></returns>
        public bool Delete(DataAccessTransaction trx)
        {
            string deleteSql = "DELETE FROM " + TableName;
            try
            {
                using (IDbCommand cmd = GetCommand(deleteSql, trx))
                {
                    bool success = cmd.ExecuteNonQuery() > 0;

                    return success;
                }
            }
            catch (Exception ex)
            {
                throw new DataAccessException(deleteSql, ex);
            }

        }

        /// <summary>
        /// Save the passed in critical errors.
        /// </summary>
        /// <param name="criticalErrors"></param>
        public void Save( List<CriticalError> criticalErrors )
        {
            using ( DataAccessTransaction trx = new DataAccessTransaction() )
            {
                Save( criticalErrors, trx );
            }
        }

        /// <summary>
        /// Save the passed in critical errors.
        /// Before insert's the new list of critical error, any old errors are deleted.
        /// </summary>
        /// <param name="criticalErrors"></param>
        /// <param name="trx"></param>
        public void Save(List<CriticalError> criticalErrors, DataAccessTransaction trx)
        {
            Delete(trx); // delete any old errors
            InsertCriticalErrors( criticalErrors, trx ); // inserts the new errors
        }

        #endregion

        #region private methods

        private CriticalError CreateFromReader(IDataReader reader, DataAccessOrdinals ordinals)
        {
            int code = SqlSafeGetInt(reader, ordinals["CODE"]);

            string description= SqlSafeGetString(reader, ordinals["DESCRIPTION"]);

            return new CriticalError(code, description);
        }

        private void InsertCriticalErrors( List<CriticalError> criticalErrors, DataAccessTransaction trx )
        {
            using (IDbCommand cmd = GetCommand("INSERT INTO " + TableName + " ( CODE, DESCRIPTION) VALUES ( @CODE, @DESCRIPTION )", trx))
            {
                foreach ( CriticalError criticalError in criticalErrors )
                {
                    cmd.Parameters.Clear();

                    cmd.Parameters.Add(GetDataParameter("@CODE", criticalError.Code));
                    cmd.Parameters.Add(GetDataParameter("@DESCRIPTION", criticalError.Description));

                    int inserted = cmd.ExecuteNonQuery();
                }
            }
        }

        #endregion 

    }
}
