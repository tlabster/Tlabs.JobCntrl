using System;

namespace Tlabs.JobCntrl {
  /// <summary>Exception thrown from operation runtime.</summary>
  public class JobCntrlException : GeneralException {
    /// <summary>Exception key in <see cref="Tlabs.JobCntrl.Model.IJobResult.ResultObjects"/></summary>
    public const string JOB_RESULT_KEY= "_#" + nameof(JobCntrlException);

    /// <summary>Default ctor</summary>
    public JobCntrlException() : base() { }

    /// <summary>Ctor from message</summary>
    public JobCntrlException(string message) : base(message) { }

    /// <summary>Ctor from message and inner exception.</summary>
    public JobCntrlException(string message, Exception e) : base(message, e) { }

  }

  /// <summary>
  /// Exception thrown on configuration errors.
  /// </summary>
  public class JobCntrlConfigException : JobCntrlException {

    /// <summary>Default ctor</summary>
    public JobCntrlConfigException() : base() { }

    /// <summary>Ctor from message</summary>
    public JobCntrlConfigException(string message) : base(message) { }

    /// <summary>Ctor from message and inner exception.</summary>
    public JobCntrlConfigException(string message, Exception e) : base(message, e) { }

  }


}
