using System;
using System.Collections.Generic;

namespace Tlabs.JobCntrl.Model.Intern {

  class JobResult : IJobResult {
    class EqComp : IEqualityComparer<IJobResult> {
      public bool Equals(IJobResult x, IJobResult y) {
        return StringComparer.OrdinalIgnoreCase.Equals(x.JobName, y.JobName);
      }
      public int GetHashCode(IJobResult obj) {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(obj.JobName);
      }
    }

    public static IEqualityComparer<IJobResult> EqComparer= new EqComp();
    private string jobName;
    private DateTime endAt= App.TimeInfo.Now;
    private string message;
    private IReadOnlyDictionary<string, object> resultObjs;
    private bool success;
    private ILog log;

    public JobResult(string name, bool success, ILog log= null) {
      this.jobName= name;
      this.IsSuccessful= success;
      this.log= log;
    }

    public JobResult(string name, string message= null, ILog log= null) : this(name, true, log) {
      this.message= message ?? this.message;
    }
    public JobResult(string name, Exception e, ILog log= null) : this(name, false, log) {
      this.message= e.ToString();
      this.resultObjs= new Dictionary<string, object> {
        [JobCntrlException.JOB_RESULT_KEY]= e
      };
    }

    public JobResult(string name, IReadOnlyDictionary<string, object> resultObjs, string message, ILog log= null) : this(name, message, log) {
      this.resultObjs= resultObjs;
    }

    public string JobName {
      get { return jobName; }
    }

    public DateTime EndAt {
      get { return endAt; ; }
    }

    public bool IsSuccessful {
      get { return success; }
      set {
        success= value;
        if (null == message) {
          message=   success
                   ? "Completed successfully."
                   : "Did not complete!";
        }
      }
    }

    public string Message { get { return message; }  }

    public IReadOnlyDictionary<string, object> ResultObjects { get { return resultObjs ?? ConfigProperties.EMPTY; } }

    public ILog ProcessingLog {
      get { return log; }
      set { log= value; }
    }

  }
}
