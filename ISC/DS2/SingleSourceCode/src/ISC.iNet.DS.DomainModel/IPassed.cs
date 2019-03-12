using System;

using System.Collections.Generic;
using System.Text;

namespace ISC.iNet.DS.DomainModel
{
    /// <summary>
    /// Events that results in a pass/failure implement this interface.
    /// </summary>
    public interface IPassed
    {
        bool Passed
        {
            get;
        }
    }
}
