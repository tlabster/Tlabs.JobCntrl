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

    public JobResult(string name, bool success) {
      this.jobName= name;
      this.IsSuccessful= success;
    }

    public JobResult(IJob operation, Exception e) : this(operation, false) {
      this.message= e.ToString();
    }

    public JobResult(IJob operation, bool success, string message) : this(operation.Name, success) {
      if (null != message || null == this.message)
        this.message= message;
    }

    public JobResult(IJob operation, IReadOnlyDictionary<string, object> resultObjs, string message) : this(operation.Name, null != resultObjs) {
      this.resultObjs= resultObjs;
      if (null != message || null == this.message)
        this.message= message;
    }

    public JobResult(IJob operation, bool success) : this(operation.Name, success) {
      var op= operation as BaseJob;
      if(null != op)
        this.log= op.Log.Log;
    }

    public JobResult(BaseJob operation, bool success, string message) : this((IJob)operation, success, message) {
      this.log= operation.Log.Log;
    }

    public JobResult(BaseJob operation, IReadOnlyDictionary<string, object> resultObjs, string message) : this((IJob)operation, resultObjs, message) {
      this.log= operation.Log.Log;
    }

    public JobResult(BaseJob operation, Exception e) : this(operation, false, e.ToString()) { }

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
