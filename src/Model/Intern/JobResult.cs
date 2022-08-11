using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tlabs.JobCntrl.Model.Intern {

  internal class JobResult : IJobResult {
    class EqComp : IEqualityComparer<IJobResult> {
      public bool Equals(IJobResult x, IJobResult y) {
        return StringComparer.OrdinalIgnoreCase.Equals(x.JobName, y.JobName);
      }
      public int GetHashCode(IJobResult obj) {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.JobName);
      }
    }

    public static IEqualityComparer<IJobResult> EqComparer= new EqComp();
    public static IReadOnlyDictionary<string, object> EMPTY_RESULT= new Dictionary<string, object>();
    readonly string jobName;
    readonly DateTime endAt = App.TimeInfo.Now;
    readonly string message;
    readonly IReadOnlyDictionary<string, object> resultObjs;
    readonly bool success;
    readonly ILog log;
    readonly Task<IJobResult> asyncRes;

    /// <summary>Ctor from <paramref name="asyncRes"/>.</summary>
    public JobResult(Task<IJobResult> asyncRes) { this.asyncRes= asyncRes; }
    /// <summary>Ctor from <paramref name="jobName"/> and <paramref name="success"/> status.</summary>
    public JobResult(string jobName, bool success, ILog log= null) : this(jobName, null, success ? EMPTY_RESULT : null, null, log) { }
    /// <summary>Ctor from <paramref name="jobName"/> and <paramref name="message"/>.</summary>
    public JobResult(string jobName, string message, ILog log= null) : this(jobName, EMPTY_RESULT, message, log) { }
    /// <summary>Ctor from <paramref name="jobName"/> and <paramref name="e"/>.</summary>
    public JobResult(string jobName, Exception e, ILog log= null) : this(jobName, e, null, null, log) { }

    /// <summary>Ctor from <paramref name="jobName"/>, <paramref name="resultObjs"/> and <paramref name="message"/>.</summary>
    public JobResult(string jobName, IReadOnlyDictionary<string, object> resultObjs, string message, ILog log= null) : this(jobName, null, resultObjs, message, log) { }

    public JobResult(string jobName, Exception e= null, IReadOnlyDictionary<string, object> resultObjs= null, string message= null, ILog log= null) {
      if (string.IsNullOrEmpty(this.jobName= jobName)) throw new ArgumentNullException(jobName);
      this.log= log;
      this.success= null == e && null != resultObjs;
      this.message= success ? message ?? $"{this.jobName} completed." : e?.Message ?? $"{this.jobName} failed.";
      this.resultObjs=   null == e
                       ? (resultObjs ?? new Dictionary<string, object>())
                       : new Dictionary<string, object>() { [JobCntrlException.JOB_RESULT_KEY]= e };
    }

    public string JobName => checkedProp(!IsAsyncResult, jobName);

    public DateTime EndAt => checkedProp(!IsAsyncResult, endAt);

    public bool IsSuccessful => checkedProp(!IsAsyncResult, success);

    public string Message => checkedProp(!IsAsyncResult, message);

    public IReadOnlyDictionary<string, object> ResultObjects => checkedProp(!IsAsyncResult, resultObjs ?? ConfigProperties.EMPTY);

    public ILog ProcessingLog => log;

    public bool IsAsyncResult => null != asyncRes;

    public Task<IJobResult> AsyncResult => checkedProp(IsAsyncResult, asyncRes);

    static T checkedProp<T>(bool chk, T prop) {
      if (!chk) throw new InvalidOperationException("Invalid async. state.");
      return prop;
    }
  }
}
