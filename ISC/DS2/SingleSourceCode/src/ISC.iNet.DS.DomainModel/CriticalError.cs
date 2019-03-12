using System;
using System.Collections.Generic;
using System.Text;

namespace ISC.iNet.DS.DomainModel
{
    /// <summary>
    /// Provides base functionality for classes defining a critical instrument error.
    /// </summary>
    public class CriticalError
    {
        /// <summary>
        /// Creates a new instance of CriticalError class.
        /// </summary>
        public CriticalError(int code, string description)
        {
            Code = code;
            Description = description;
        }

        /// <summary>
        /// Gets or Sets Critical Instrument Error Code
        /// </summary>
        public int Code { get; set; }

        /// <summary>
        /// Gets or Sets Critical Instrument Error description
        /// </summary>
        public string Description { get; set; }
    }
}
