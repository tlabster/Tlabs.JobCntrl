using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Tlabs.JobCntrl.Model.Intern {
  using static Tlabs.Misc.Safe;
  using IJobProps = IReadOnlyDictionary<string, object>;


  /// <summary><see cref="IJob"/> master class.</summary>
  /// <remarks>Instances of this class are factory classes for actual <see cref="IJob"/> implementations.</remarks>
  public class MasterJob : MasterBase<IJob> {
    internal static readonly ILogger Log= App.Logger<IJob>();

    /// <summary>Ctor.</summary>
    /// <param name="name">unique (Job) master name</param>
    /// <param name="description">description string</param>
    /// <param name="jobType">Target <see cref="IJob"/> instance type</param>
    /// <param name="properties">Job master configuration parameters</param>
    public MasterJob(string name, string description, Type jobType, IJobProps properties)
      : base(name, description, jobType, properties) { }

    /// <summary>Create an <see cref="IJob"/> runtime instance.</summary>
    /// <param name="jobStarter"><see cref="IStarter"/> that is starting the runtime job.</param>
    /// <param name="name">Job's runtime name</param>
    /// <param name="description">description</param>
    /// <param name="properties">Job's runtime configuration parameters</param>
    /// <returns></returns>
    public IJob CreateRuntimeJob(IStarter jobStarter, string name, string description, IJobProps properties) {
      return new RuntimeJob(this, jobStarter, name, description, properties);
    }

    /// <summary>dispose when <param name="disposing"/> == true.</summary>
    protected override void Dispose(bool disposing) { }

    /// <summary>Job runtime (proxy)</summary>
    /// <remarks>
    /// <para>Objects of this class are created from a <see cref="MasterJob"/> and serve as a proxy to the
    /// actual Job implementation.</para>
    /// <para>It's purpose is to register on behalf of the Job at the <see cref="IStarter.Activate"/> event and
    /// create a new Job runtime instance on every Starter activation to run this instance asynchronously.
    /// After the <see cref="IJob.Run(IJobProps)"/> method of the actual Job has finished, it's <see cref="IJobResult"/>
    /// gets reported to the activating Starter.</para>
    /// </remarks>
    sealed class RuntimeJob : BaseModel, IJob {
      private MasterJob masterJob;
      private IStarter jobStarter;
      private StarterActivator activationHandler;

      internal RuntimeJob(MasterJob masterJob, IStarter jobStarter, string name, string description, IJobProps properties)
        : base(name, description, new ConfigProperties(masterJob.Properties, properties).AsReadOnly()) {
        this.masterJob= masterJob;
        this.jobStarter= jobStarter;

        this.jobStarter.Activate+= (this.activationHandler= HandleStarterInvocation);
        MasterJob.Log.LogDebug("Job[{JP}] registered for activation by starter[{ST}].", this.Name, this.jobStarter.Name);
      }

      public ILog ProcessingLog => throw new NotImplementedException();

      public IJob Initialize(string name, string description, IJobProps properties) {
        throw new InvalidOperationException("Already initalized");
      }

      public IJobResult Run(IJobProps props) {
        throw new InvalidOperationException("Can only be run by IActivator invocation.");
      }

      protected override void Dispose(bool disposing) {
        if (!disposing) return;
        if (null != jobStarter) {
          if (null != activationHandler) {
            jobStarter.Activate-= activationHandler;
            MasterJob.Log.LogDebug("Job[{JP}] de-registered from starter[{ST}].", Name, jobStarter.Name);
          }
        }
        activationHandler= null;
        jobStarter= null;
        masterJob= null;
      }


      private bool HandleStarterInvocation(IStarter srcStarter, IJobProps runProps) {
        MasterJob.Log.LogInformation("Executing job '{JOB}' (activated from starter[{ST}], jobType: {JT}).", this.Name, srcStarter.Name, masterJob.targetType.Name);
        var jobStarter= (IStarterActivation)srcStarter;   //must be activated from starter
        IJob asyncJob= null;

        Tlabs.App.RunBackgroundService<IJob, IJobResult>(masterJob.targetType, job => {
          asyncJob= job.Initialize(this.Name, this.Description, this.Properties);
          return asyncJob.Run(runProps);
        })
        .ContinueWith(async jobTsk => {   // and continue with job result
          IJobResult jobResult= null;
          try {
            jobResult= await jobTsk;
            while (jobResult.IsAsyncResult)   // support async. job result(s)
              jobResult= await jobResult.AsyncResult;   //unwrap async result
          }
          catch (Exception e) {
            jobResult= new JobResult(this?.Name, e, asyncJob?.ProcessingLog);
          }
          try {
            Log.LogInformation("Job '{J}' {RES1}.", jobResult.JobName, jobResult.IsSuccessful ? "finished successfully" : "failed");
            if (!jobResult.IsSuccessful) {
              Log.LogError("Execution problem from job '{JP}': {MSG}", jobResult.JobName, jobResult.Message);
              var procLog= jobResult.ProcessingLog;
              if (null != procLog && Log.IsEnabled(LogLevel.Debug)) foreach (var log in procLog.Entries) //push job log to global log
                if (Log.IsEnabled(LogLevel.Debug)) Log.LogDebug("{TM} {STEP}> {MSG}", $"{log.ElapsedMsec:D3} {procLog.EntryTime(log):HH:mm:ss,FFF}", log.ProcessStep, log.Message);
            }
          }
          catch (Exception e) {
            //Make sure we always add a result to the starter - else the starter would hang !
            jobResult= new JobResult(this?.Name, e, null);
            MasterJob.Log.LogError(e, "Error returning job {JOB}'s result ({MSG}).", this?.Name, e?.Message);
          }
          //Make sure we always add a result to the starter - else the starter would hang !
          jobStarter.AddResult(jobResult);
        });
        return true;  //all job activations always return true
      }//HandleStarterInvocation

    }//class RuntimeJob

  }//class MasterJob

}
