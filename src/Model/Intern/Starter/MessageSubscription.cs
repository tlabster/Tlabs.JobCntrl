using System;
using Microsoft.Extensions.Logging;
using Tlabs.Msg;

namespace Tlabs.JobCntrl.Model.Intern.Starter {
  
  /// <summary>Starter that activates on published messages.</summary>
  /// <remarks>
  /// <para>The starter subscribes on a message subject specified by the <c>PROP_MSG_SUBJECT</c> config. property.
  /// The published message gets passed as run-properties.</para>
  /// </remarks>
  public class MessageSubscription : BaseStarter {
    /// <summary>Name of a property that specifies the message subject.</summary>
    public const string PROP_MSG_SUBJECT= "Message-Subject";

    private static readonly ILogger log= App.Logger<MessageSubscription>();

    private IMessageBroker msgBroker;
    private string subscriptionSubject;
    private Action<BackgroundJobMessage> messageHandlerDelegate;

    /// <summary>Ctor from <paramref name="msgBroker"/>.</summary>
    public MessageSubscription(IMessageBroker msgBroker) {
      this.msgBroker= msgBroker;
    }

    /// <summary>Internal starter initialization.</summary>
    protected override IStarter InternalInit() {
      this.subscriptionSubject= PropertyString(PROP_MSG_SUBJECT);
      log.LogInformation("Message subscription starter '{name}' for subject [{sbj}] initialzed.", Name, subscriptionSubject);
      ChangeEnabledState(this.isEnabled);
      return this;
    }

    /// <summary>Changes the enabled state of the starter according to <paramref name="enabled"/>.</summary>
    [System.Security.SecurityCritical]
    protected override void ChangeEnabledState(bool enabled) {
      lock(msgBroker) {
        if (   true == (this.isEnabled= enabled)) {
          if (null == messageHandlerDelegate)
            msgBroker.Subscribe<BackgroundJobMessage>(subscriptionSubject, this.messageHandlerDelegate= this.messageHandler);
          return;
        }
        msgBroker.Unsubscribe(messageHandlerDelegate);
      }
      log.LogDebug("Message subscription starter '{name}' enabled: {state}.", Name, isEnabled);
    }

    private void messageHandler(BackgroundJobMessage message) {
      log.LogDebug("Message subscription starter '{name}' activation from soruce: {source}.", Name, message.Source);
      this.DoActivate(message.JobProperties);
    }


    /// <summary>Dispose managed resources on <paramref name="disposing"/> == true.</summary>
    protected override void Dispose(bool disposing) {
      base.Dispose(disposing);
      if (null == msgBroker) return;
      ChangeEnabledState(false);
      msgBroker= null;
    }
  }
}
