using System;
using System.Collections.Generic;

namespace Tlabs.JobCntrl {

  /// <summary>Message to be sent to a <see cref="Model.Intern.Starter.MessageSubscription"/>.</summary>
  public class BackgroundJobMessage {

    /// <summary>Message source/origin (informational).</summary>
    public string Source { get; }

    /// <summary>Run-properties to be passed to the background job(s).</summary>
    public IReadOnlyDictionary<string, object> JobProperties { get; }
  }
}