using System;
using System.Collections.Generic;

namespace Tlabs.JobCntrl.Model {
  using IJobProps= IReadOnlyDictionary<string, object>;

  /// <summary>JobControl Job.</summary>
  public interface IJob : IModel {

    /// <summary>Initialize a Job</summary>
    /// <param name="name">Unique Job name/ID</param>
    /// <param name="description">Job description</param>
    /// <param name="properties">Properties dictionary</param>
    /// <returns>*this*. (Utility to create instances like: <c>var job= new Job().Initialize(name, description, properties);</c>)</returns>
    IJob Initialize(string name, string description, IJobProps properties);

    /// <summary>Run Job</summary>
    IJobResult Run(IJobProps props);

  }

  /// <summary>Result of a Job run.</summary>
  public interface IJobResult {

    /// <summary>Job name.</summary>
    string JobName { get; }

    /// <summary>Time of Job end.</summary>
    DateTime EndAt { get; }

    /// <summary>Job's success status.</summary>
    bool IsSuccessful { get; }

    /// <summary>Job's result status message.</summary>
    string Message { get; }

    /// <summary>Optional JobControl result object(s).</summary>
    IReadOnlyDictionary<string, object> ResultObjects { get; }

    /// <summary>Job's processing log.</summary>
    ILog ProcessingLog { get; }

  }


  /// <summary>Interface of an object that lets a Job produce an result for that a consumer can wait.</summary>
  public interface IJobResultProducer<T> {

    /// <summary>True if a result has been produced.</summary>
    /// <remarks>if this reurn true, a subsequent call to <see cref="GetResult()"/> would return a result immediately.</remarks>
    bool HasResult { get; }

    /// <summary>Produce a result to be returned from <see cref="GetResult()"/>.</summary>
    void Produce(T result);

    /// <summary>Returns a produced result.</summary>
    /// <remarks>Blocks until a result has been produced.</remarks>
    T GetResult();

    /// <summary>Returns a result produced.</summary>
    /// <remarks>Blocks until a result has been produced or timed-out.</remarks>
    /// <param name="timeOut">Max. number of milliseconds to wait for a result.</param>
    /// <exception cref="TimeoutException">
    /// Exception thrown when <paramref name="timeOut"/> milliseconds elapsed w/o resilt.
    /// </exception>
    T GetResult(int timeOut);

  }

  /// <summary>Abstract Job base class.</summary>
  /// <remarks>Convenients base class for implementators of the <see cref="IJob"/> interface.</remarks>
  public abstract class BaseJob : BaseModel, IJob {
    /// <summary>Log-level property name.</summary>
    public const string PROP_LOGLEVEL= "Log-Level";

    /// <summary>Job logger.</summary>
    protected IJobLogger log;

    /// <summary>Dump (Job) log to standard out stream (console).</summary>
    public static void DumpLog(ILog log) {
      foreach (var le in log.Entries)
        Console.WriteLine("{0:D3} {1:HH:mm:ss,FFF} {2}> {3}", le.ElapsedMsec, log.EntryTime(le), le.ProcessStep, le.Message);
    }

    /// <summary>Initialize a Job</summary>
    /// <param name="name">Unique Job name/ID</param>
    /// <param name="description">Job description</param>
    /// <param name="properties">Properties dictionary</param>
    /// <returns>*this*. (Utility to create instances like: <c>var Job= new Job().Initialize(name, description, properties);</c>)</returns>
    public IJob Initialize(string name, string description, IJobProps properties) {
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
    protected abstract IJobResult InternalRun(IDictionary<string, object> runProperties);

    /// <summary>Create a <see cref="IJobResult"/> with the success status.</summary>
    protected IJobResult CreateResult(bool success) {
      return new Intern.JobResult(this.Name, success, this.log.Log);
    }

    /// <summary>Create a <see cref="IJobResult"/> with <paramref name="resultObjs"/> and message.</summary>
    protected IJobResult CreateResult(IDictionary<string, object> resultObjs, string message) {
      return new Intern.JobResult(this.Name, resultObjs.AsReadOnly(), message, this.log.Log);
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
  }//class AbstractJob
}
