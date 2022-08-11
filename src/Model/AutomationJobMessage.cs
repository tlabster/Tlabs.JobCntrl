using System;
using System.Collections.Generic;

namespace Tlabs.JobCntrl.Model {

  /// <summary>Message to be sent to a <see cref="Model.Intern.Starter.MessageSubscription"/>.</summary>
  public class AutomationJobMessage : Misc.ICloneable<AutomationJobMessage> {
    /// <summary>Name of a property that specifies the message object.</summary>
    public const string PROP_MSG_OBJ= "Msg-Object";

    /// <summary>Ctor from <paramref name="source"/> and optional <paramref name="messageObj"/> or <paramref name="optionalProps"/>.</summary>
    public AutomationJobMessage(object source, object messageObj= null, IReadOnlyDictionary<string, object> optionalProps= null) {
      this.Source= source;
      this.JobProperties= new ConfigProperties(optionalProps ?? ConfigProperties.EMPTY){ [PROP_MSG_OBJ]= messageObj };
    }

    /// <summary>Message source/origin (informational).</summary>
    public object Source { get; }

    ///<summary>Run-properties to be passed to the automation job(s).</summary>
    public IReadOnlyDictionary<string, object> JobProperties { get; }

    /// <summary>Message object.</summary>
    public object MsgObject => JobProperties.TryGetValue(PROP_MSG_OBJ, out var msg) ? msg : null;

    ///<inheritdoc/>
    public AutomationJobMessage Clone() => (AutomationJobMessage)this.MemberwiseClone();
  }
}