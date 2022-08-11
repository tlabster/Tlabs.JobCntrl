using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Tlabs.JobCntrl.Model.Intern.Starter {

  /// <summary>Starter to activate when a preceding starter has completed.</summary>
  /// <remarks>The <see cref="Chained"/> operates in two general modes which are controlled with
  /// the property <see cref="Chained.PROP_ACTIVATE_ON_PREV_STATUS"/>:
  /// <para>Success<br/>
  /// The starter activates only if *ALL* the preceding jobs succeeded. If the property <see cref="Chained.PROP_PREVIOUS_ALLOW_FAIL"/>
  /// is set to true, not all of the previous job results are required to succeed, but only the successful results get
  /// propagated.
  /// </para>
  /// <para>Failure<br/>
  /// Activates only if at least one of the previous job's results failed. In that case it propagates all failed job results
  /// with the job name.
  /// </para>
  /// </remarks>
  public class Chained : BaseStarter {
    /// <summary>Prop. name for predecessor starter.</summary>
    public const string PROP_COMPLETED_STARTER= "Completed-Starter";
    /// <summary>Prop. name to specify failure behavior on predecessor.</summary>
    public const string PROP_PREVIOUS_ALLOW_FAIL= "Prev-Allow-Fail";
    /// <summary>Prop. name to specify the required result status of the predecessor.</summary>
    public const string PROP_ACTIVATE_ON_PREV_STATUS= "Activate-On-Previous-Status";
    /// <summary>Prop. name for redecessor starter completion.</summary>
    public const string RPROP_STARTER_COMPLETION= "$Starter-Completion";
    /// <summary>Prop. name for redecessor result properties.</summary>
    public const string RPROP_PREVIOUS_RESULTS= "$Previous-Results";

    /// <summary>Enumeration of previous job states.</summary>
    public enum PreviousJobStatus {
      /// <summary>job run succeeded.</summary>
      Success,
      /// <summary>job run failed.</summary>
      Failure
    };

    private IRuntimeStarter completedStarter;
    private bool previousAllowFail;
    private PreviousJobStatus activateOnPreviousStatus;
    private StarterActivationCompleter completionDelegate;

    ///<inheritdoc/>
    protected override IStarter InternalInit() {
      if (string.IsNullOrEmpty(Properties[PROP_COMPLETED_STARTER] as string)) throw new JobCntrlConfigException(PROP_COMPLETED_STARTER + " property missing");
      if (null == Properties[MasterStarter.PROP_RUNTIME]) throw new JobCntrlConfigException(MasterStarter.PROP_RUNTIME + " property missing");
      this.previousAllowFail= PropertyBool(PROP_PREVIOUS_ALLOW_FAIL, false);
      this.activateOnPreviousStatus= (PreviousJobStatus)PropertyEnum(PROP_ACTIVATE_ON_PREV_STATUS, PreviousJobStatus.Success);
      this.completionDelegate= HandlePrecedingStarterCompletion;
      return this;
    }

    ///<inheritdoc/>
    protected override void ChangeEnabledState(bool enabled) {
      if (true == (this.isEnabled= enabled)) {
        if (null == this.completedStarter) {
          if (Properties[MasterStarter.PROP_RUNTIME] is IJobControl runtime) {
            var starterName= Properties[PROP_COMPLETED_STARTER] as string ?? "?";
            this.completedStarter= (IRuntimeStarter)runtime.Starters[starterName];
          }
        }
        if(true == (this.isEnabled= null != this.completedStarter))
          completedStarter.ActivationComplete+= completionDelegate;
      }
      else if(null != this.completedStarter)
        completedStarter.ActivationComplete-= completionDelegate;
    }

    private void HandlePrecedingStarterCompletion(IStarterCompletion precedingCompletion) {
      /* Prepare the runProps for the chained starter to be activated:
       */
      var runProps = new ConfigProperties(precedingCompletion.RunProperties ?? ConfigProperties.EMPTY) {
        [RPROP_STARTER_COMPLETION]= precedingCompletion
      };
      var prevResults= (IDictionary<string, object>)ConfigProperties.GetOrSet(runProps, RPROP_PREVIOUS_RESULTS, new ConfigProperties());

      var successResults= new Dictionary<string, object>();
      var jobFailures= new Dictionary<string, object>();

      /* Collect preceding results/failures:
       */
      foreach (var result in precedingCompletion.JobResults) {
        if (result.IsSuccessful)
          successResults.SetRange(result.ResultObjects);
        else
          jobFailures.Add(result.JobName, result);
      }

      switch (activateOnPreviousStatus) {

        case PreviousJobStatus.Success:
          if ( !previousAllowFail && jobFailures.Count > 0) {
            if (MasterStarter.Log.IsEnabled(LogLevel.Information)) {
              IEnumerable<string> failedJobs= jobFailures.Keys;
              MasterStarter.Log.LogInformation("Preceding completion failed for '{A}' - activation disabled.", string.Join(", ", failedJobs));
            }
            return; //One ore more previous jobs failed: do not acivate and propagate any job results
          }
          /* Propagate success results only, ignore any failures:
           */
          prevResults.SetRange(successResults);
        break;
        
        case PreviousJobStatus.Failure:
          if (jobFailures.Count == 0) return; //do not acivate w/o any failure
          prevResults.SetRange(jobFailures);
        break;

        default: throw new InvalidOperationException($"Unexpected previous job result-status: {activateOnPreviousStatus}");
      }

      DoActivate(runProps);
    }
  
  }

}
