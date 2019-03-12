using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace ISC.iNet.DS.DataAccess
{
    /// <summary>
    /// Provide's caching of a DataReader's column ordinals.  Use of this class
    /// provides about 25 - 30% performance improvement when reading database data
    /// using the SQLiteDataReader.
    /// </summary>
    /// <remarks>
    /// This class is provided because SQLiteDataReader.GetOrdinal is extremely slow
    /// due the way it's implemented (sequentially scanning an array).
    /// GetOrdinal is called whenever common 'DataReader[ "MY_COLUMN" ]' calls are made.
    /// (e.g. "int id = myDbReader[ "ID" ].)
    /// <para>
    /// So, before pulling data out of an SQLiteDataReader that may contain multiple records,
    /// it's recommended to first instantiate an Ordinal to cache up all the desired
    /// column's ordinals, then use the Ordinal instance when getting data out of the reader.
    /// </para>
    /// <para>
    /// A most basic example...
    /// </para>
    /// <code>
    /// IDbReader reader = cmd.ExecuteReader();
    ///
    /// DataAccessOrdinals ordinals = new DataAccessOrdinals( reader );
    ///
    /// while ( reader.Read() )
    /// {
    ///     long id = (long)reader[ ordinals[ "ID" ] ];
    ///     string partnumber = (string)reader[ ordinals[ "PARTNUMBER" ] ];
    ///     string description = (string)reader[ ordinals[ "DESCRIPTION" ] ];
    /// }
    /// </code>
    /// </remarks>
    public class DataAccessOrdinals
    {
        private Dictionary<string, int> _ordinals;

        internal DataAccessOrdinals( IDataReader reader )
        {
            _ordinals = new Dictionary<string, int>( reader.FieldCount );

            for ( int i = 0; i < reader.FieldCount; i++ )
            {
                string name = reader.GetName( i );
                _ordinals[ name ] = i;
            }
        }

        internal int this[ string columnName ]
        {
            get { return _ordinals[ columnName ]; }
        }
    }
}
