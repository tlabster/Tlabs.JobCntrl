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

    /// <summary>Job's processing log.</summary>
    ILog ProcessingLog { get; }
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

}
