﻿using System;
using System.Collections.Generic;

namespace Tlabs.JobCntrl.Model.Intern {

  ///<summary>Internal job logger implementation.</summary>
  ///<remarks>NOTE:
  /// If the log entry limit is exeeded, the entry count is shrinked by removing entries
  /// with lower priority except problem entries...
  ///</remarks>
  class JobLogger : IJobLogger {
    internal const string DEFAULT_PROCSTEP= ".";
    internal const int DEFAULT_LIMIT= 1000;
    private string currentProcStep= DEFAULT_PROCSTEP;
    readonly JobLog log;


    public JobLogger(JobLogLevel level) : this(level, DEFAULT_LIMIT) { }

    public JobLogger(JobLogLevel level, int logLimit) { this.log= new JobLog(level, logLimit); }

    public JobLogger(string levelName) {
      var isLevelName= Enum.TryParse<JobLogLevel>(levelName, true, out var level);

      this.log= new JobLog(level, DEFAULT_LIMIT);
      if (false == isLevelName)
        ProblemFormat("Unrecognized log detail '{0}' - default to '{1}'", levelName, level);
    }

    public string ProcessStep {
      get { return currentProcStep; }
      set { currentProcStep= string.IsNullOrEmpty(value) ? DEFAULT_PROCSTEP : value; }
    }

    public void Problem(string message) {
      log.AddLast(new JobLog.Entry(this, JobLogLevel.Problem, message));
      ++log.ProblemCount;
    }

    public void ProblemFormat(string format, object arg1) {
      log.AddLast(new JobLog.Entry(this, JobLogLevel.Problem, string.Format(App.DfltFormat,format, arg1)));
      ++log.ProblemCount;
    }

    public void ProblemFormat(string format, object arg1, object arg2) {
      log.AddLast(new JobLog.Entry(this, JobLogLevel.Problem, string.Format(App.DfltFormat,format, arg1, arg2)));
      ++log.ProblemCount;
    }

    public void ProblemFormat(string format, params object[] args) {
      log.AddLast(new JobLog.Entry(this, JobLogLevel.Problem, string.Format(App.DfltFormat,format, args)));
      ++log.ProblemCount;
    }

    public void Info(string message) {
      if (log.Level >= JobLogLevel.Info)
        log.AddLast(new JobLog.Entry(this, JobLogLevel.Info, message));
    }

    public void InfoFormat(string format, object arg1) {
      if (log.Level >= JobLogLevel.Info)
        log.AddLast(new JobLog.Entry(this, JobLogLevel.Info, string.Format(App.DfltFormat,format, arg1)));
    }

    public void InfoFormat(string format, object arg1, object arg2) {
      if (log.Level >= JobLogLevel.Info)
        log.AddLast(new JobLog.Entry(this, JobLogLevel.Info, string.Format(App.DfltFormat,format, arg1, arg2)));
    }

    public void InfoFormat(string format, params object[] args) {
      if (log.Level >= JobLogLevel.Info)
        log.AddLast(new JobLog.Entry(this, JobLogLevel.Info, string.Format(App.DfltFormat,format, args)));
    }

    public void Detail(string message) {
      if (log.Level >= JobLogLevel.Detail)
        log.AddLast(new JobLog.Entry(this, JobLogLevel.Detail, message));
    }

    public void DetailFormat(string format, object arg1) {
      if (log.Level >= JobLogLevel.Detail)
        log.AddLast(new JobLog.Entry(this, JobLogLevel.Detail, string.Format(App.DfltFormat,format, arg1)));
    }

    public void DetailFormat(string format, object arg1, object arg2) {
      if (log.Level >= JobLogLevel.Detail)
        log.AddLast(new JobLog.Entry(this, JobLogLevel.Detail, string.Format(App.DfltFormat,format, arg1, arg2)));
    }

    public void DetailFormat(string format, params object[] args) {
      if (log.Level >= JobLogLevel.Detail)
        log.AddLast(new JobLog.Entry(this, JobLogLevel.Detail, string.Format(App.DfltFormat,format, args)));
    }

    public ILog Log { get { return log; } }


    private class JobLog : LinkedList<ILogEntry>, ILog {
      readonly DateTime startAt= DateTime.UtcNow;
      readonly int limit;
      private JobLogLevel level;
      public int ProblemCount;

      public JobLog(JobLogLevel level, int logLimit) { this.level= level; this.limit= logLimit; }

      public DateTime StartAt { get { return startAt; } }

      public JobLogLevel Level { get { return level; } }

      public bool IsLogLevel(JobLogLevel level) { return (this.level >= level); }

      public bool HasProblem { get { return ProblemCount > 0; } }

      public DateTime EntryTime(ILogEntry logEntry) {
        var dt= new DateTime(startAt.Ticks + logEntry.ElapsedMsec * TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
        return App.TimeInfo.ToAppTime(dt);
      }

      public IEnumerable<ILogEntry> Entries { get { return this; } }

      public class Entry : ILogEntry {
        readonly int elapsed;
        readonly JobLogLevel level;
        readonly string procStep;
        readonly string msg;

        public Entry(JobLogger logger, JobLogLevel level, string msg) {
          JobLog log= logger.log;
          this.elapsed= (int)((DateTime.UtcNow.Ticks - log.StartAt.Ticks) /TimeSpan.TicksPerMillisecond);
          this.level= level;
          this.procStep= logger.currentProcStep;
          this.msg= msg;

          for (var lev= log.level; lev != JobLogLevel.Problem && log.Count >= log.limit; ) {
            ShrinkLogByLevel(log, --lev);
          }
        }

        public int ElapsedMsec { get { return elapsed; } }

        public JobLogLevel Level { get { return level; } }

        public string ProcessStep { get { return procStep; } }

        public string Message { get { return msg; } }

        /* Shrink the log from start (oldest) by removing any entries with level 'lev'.
         */
        private static void ShrinkLogByLevel(JobLog log, JobLogLevel lev) {
          log.level= lev;
          LinkedListNode<ILogEntry>? next;
          for (var node= log.First; null != node; node= next) {
            next= node.Next;
            if (lev < node.Value.Level)
              log.Remove(node);
          }
        }

      }
    }
  }
}
