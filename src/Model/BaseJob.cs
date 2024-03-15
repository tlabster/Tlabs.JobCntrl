using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tlabs.JobCntrl.Model {
  using IJobProps= IReadOnlyDictionary<string, object?>;

  /// <summary>Abstract Job base class.</summary>
  /// <remarks>Convenients base class for implementators of the <see cref="IJob"/> interface.</remarks>
  public abstract class BaseJob : BaseModel, IJob {
    /// <summary>Log-level property name.</summary>
    public const string PROP_LOGLEVEL= "Log-Level";

    /// <summary>Job logger.</summary>
    protected IJobLogger log= null!;

    /// <summary>Dump Job log to standard out stream (console).</summary>
    public static void DumpLog(BaseJob job) => DumpLog(job.log.Log);

    /// <summary>Dump log to standard out stream (console).</summary>
    public static void DumpLog(ILog log) {
      foreach (var le in log.Entries)
        Console.WriteLine("{0:D3} {1:HH:mm:ss,FFF} {2}> {3}", le.ElapsedMsec, log.EntryTime(le), le.ProcessStep, le.Message);
    }

    /// <summary>Initialize a Job</summary>
    /// <param name="name">Unique Job name/ID</param>
    /// <param name="description">Job description</param>
    /// <param name="properties">Properties dictionary</param>
    /// <returns>*this*. (Utility to create instances like: <c>var Job= new Job().Initialize(name, description, properties);</c>)</returns>
    public IJob Initialize(string name, string? description, IJobProps? properties) {
      InitBase(name, description, properties);
      log= InternalCreateLogger();
      return InternalInit();
    }

    /// <summary>Run Job</summary>
    public IJobResult Run(IJobProps props) {
      return InternalRun((new ConfigProperties(Properties, props)));
    }

    /// <summary>Log logger.</summary>
    public IJobLogger Log { get { return log; } }

    /// <inheritdoc/>
    public ILog ProcessingLog => Log.Log;

    /// <summary>Internal Job initalization.</summary>
    /// <remarks>
    /// <para>Called from <c>Initialize()</c> after name, description and properties have been set. Implementations
    /// should use this method to initialize non-property specific Job state.</para>
    /// <para>Note: The final set of Job properties is available only at the actual Job run, where additional run properties
    /// from the starter activation might be specified.</para>
    /// </remarks>
    protected abstract IJob InternalInit();

    /// <summary>Internal Job run method.</summary>
    /// <param name="runProperties">
    /// Properties dictionary already containing all properties (merged with run-properties).
    /// <par>Note: Other than the <see cref="IModel.Properties"/>, the <paramref name="runProperties"/> are not read-only.</par>"/>
    /// </param>
    /// <remarks>To be implemented with Job functionality.</remarks>
    protected abstract IJobResult InternalRun(IJobProps runProperties);

    /// <summary>Create a <see cref="IJobResult"/> with the success status.</summary>
    protected IJobResult CreateResult(bool success) {
      return new Intern.JobResult(this.Name, success, this.log.Log);
    }

    /// <summary>Create a <see cref="IJobResult"/> with <paramref name="resultObjs"/> and (optional) <paramref name="message"/>.</summary>
    protected IJobResult CreateResult(IReadOnlyDictionary<string, object?> resultObjs, string? message= null) {
      return new Intern.JobResult(this.Name, resultObjs, message, this.log.Log);
    }

    /// <summary>Create async <see cref="IJobResult"/> from <paramref name="resTsk"/> with callback <paramref name="buildJobResult"/>.</summary>
    protected IJobResult CreateAsyncResult<TRes>(Task<TRes> resTsk, Func<TRes, IJobResult> buildJobResult) {
      var cmplSrc= new TaskCompletionSource<IJobResult>();
      resTsk.ContinueWith(async tsk => {
        try {
          var res= await tsk;
          cmplSrc.TrySetResult(buildJobResult(res));
        }
        catch (Exception e) {
          cmplSrc.TrySetResult(CreateResult(e));
        }
      });
      return new Intern.JobResult(cmplSrc.Task);
    }

    /// <summary>Create a <see cref="IJobResult"/> from exception.</summary>
    protected IJobResult CreateResult(Exception e) {
      this.log.ProblemFormat("Job aborted: {0}", e.Message);
      this.log.ProcessStep= "ABORT";
      return new Intern.JobResult(this.Name, e, this.log.Log);
    }

    /// <summary>Create a <see cref="IJobLogger"/> implementation.</summary>
    protected virtual IJobLogger InternalCreateLogger() {
      string levelName= PropertyString(PROP_LOGLEVEL, "?");
      return new Intern.JobLogger(levelName);
    }
  }//class BaseJob

}
