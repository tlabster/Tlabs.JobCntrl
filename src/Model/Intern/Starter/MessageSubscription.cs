using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tlabs.Msg;

namespace Tlabs.JobCntrl.Model.Intern.Starter {

  /// <summary>Starter that activates on published messages.</summary>
  /// <remarks>
  /// <para>The starter subscribes on a message subject specified by the <c>PROP_MSG_SUBJECT</c> config. property.
  /// The published message gets passed as run-properties.</para>
  /// <para>If a buffer time (millisec.) was specified, multiple messages published within that buffer time are buffered into one single activation.</para>
  /// </remarks>
  public class MessageSubscription : BaseStarter {
    /// <summary>Name of a property that specifies the message subject.</summary>
    public const string PROP_MSG_SUBJECT= "Message-Subject";
    /// <summary>Name of a property that specifies a optional buffer time in miliseconds.</summary>
    public const string PROP_BUFFER= "Buffer-Ms";

    private static readonly ILogger log= App.Logger<MessageSubscription>();

    private IMessageBroker msgBroker;
    private string subscriptionSubject;
    private int buffer;
    private Task bufferTask;
    private CancellationTokenSource cancelSource;
    private Action<BackgroundJobMessage> messageHandlerDelegate;

    /// <summary>Ctor from <paramref name="msgBroker"/>.</summary>
    public MessageSubscription(IMessageBroker msgBroker) {
      this.msgBroker= msgBroker;
    }

    /// <summary>Internal starter initialization.</summary>
    protected override IStarter InternalInit() {
      this.subscriptionSubject= PropertyString(PROP_MSG_SUBJECT, "--undefined--");
      log.LogInformation("Message subscription starter '{name}' for subject [{sbj}] initialzed.", Name, subscriptionSubject);
      if (0 != (this.buffer= PropertyInt(PROP_BUFFER, 0)))
        log.LogInformation("Message buffer '{buffer}'ms.", this.buffer);

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
      if (0 == buffer) {
        doActivateWithMessage(message);
        return;
      }

      setCancelSource();
      bufferTask= Task.Delay(buffer, cancelSource.Token);
      bufferTask.ContinueWith(t => {
        t.Dispose();
        cancelSource?.Dispose();
        cancelSource= null;
        doActivateWithMessage(message);
      }, TaskContinuationOptions.NotOnCanceled);
    }

    private void setCancelSource() {
      CancellationTokenSource cts0, cts;
      if (null != (cts= Interlocked.CompareExchange<CancellationTokenSource>(ref cancelSource, cts0= new CancellationTokenSource(), null))) try {
        cts.Cancel(); //could throw if already disposed
        cts.Dispose();
        bufferTask.Dispose();
        if (cts != Interlocked.CompareExchange<CancellationTokenSource>(ref cancelSource, cts0, cts))
          cts0.Dispose(); // let other win;
      }
      catch (Exception e) when (Misc.Safe.NoDisastrousCondition(e)) { }
    }

    private void doActivateWithMessage(BackgroundJobMessage message) {
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
