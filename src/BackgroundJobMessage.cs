using System;
using System.Collections.Generic;

namespace Tlabs.JobCntrl {

  /// <summary>Message to be sent to a <see cref="Model.Intern.Starter.MessageSubscription"/>.</summary>
  public class BackgroundJobMessage : Misc.ICloneable<BackgroundJobMessage> {

    /// <summary>Message source/origin (informational).</summary>
    public object Source { get; set;}

    ///<summary>Run-properties to be passed to the background job(s).</summary>
    public IReadOnlyDictionary<string, object> JobProperties { get; set; }
    
    ///<inheritdoc/>
    public BackgroundJobMessage Clone() => (BackgroundJobMessage)this.MemberwiseClone();
  }
}