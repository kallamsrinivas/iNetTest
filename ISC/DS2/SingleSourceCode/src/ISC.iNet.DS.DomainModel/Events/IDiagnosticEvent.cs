using System;

using System.Collections.Generic;
using System.Text;

namespace ISC.iNet.DS.DomainModel
{
    /// <summary>
    /// Tags an event as a Diagnostics event.
    /// </summary>
    public interface IDiagnosticEvent
    {
        /// <summary>
        /// Gets or sets the list of diagnostics events.
        /// </summary>
        List<Diagnostic> Diagnostics { get; set; }
    }
}
