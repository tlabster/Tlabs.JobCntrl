using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tlabs.JobCntrl.Model {

  /// <summary>Enumeration of log severity levels.</summary>
  public enum JobLogLevel : byte {
    /// <summary>Problem log level.</summary>
    Problem,
    /// <summary>Informational log level.</summary>
    Info,
    /// <summary>Detail log level.</summary>
    Detail
  }

  /// <summary>Representation of a logging facility.</summary>
  /// <remarks><para>
  /// A ILogger generates entries in a <see cref="ILog"/>. Entries are categorized by three levels of severity:</para>
  /// <para>
  /// 'problem', 'info'(-rmational) and 'detail'. Depending on the current log-level of the <see cref="ILog"/> an entry
  /// gets added to the log or is discarded...
  /// </para>
  /// <para>The <see cref="IJobLogger.ProcessStep"/> denotes a group of operations whose log entries are marked with a common
  /// processing step name. The ProcessStep property should be changed accordingly when ever a new processing step is started.</para>
  /// </remarks>
  public interface IJobLogger {
    /// <summary>
    /// Denotes a group of operations whose log entries are marked with a common
    /// processing step name.
    /// </summary>
    string ProcessStep { get; set; }

    /// <summary>Add a problem message to the log.</summary>
    void Problem(string message);

    /// <summary>Add a formated problem message to the log.</summary>
    void ProblemFormat(string format, object arg1);
    /// <summary>Add a formated problem message to the log.</summary>
    void ProblemFormat(string format, object arg1, object arg2);
    /// <summary>Add a formated problem message to the log.</summary>
    void ProblemFormat(string format, params object[] args);

    /// <summary>Add a info(-rmational) message to the log.</summary>
    void Info(string message);

    /// <summary>Add a formated info(-rmational) message to the log.</summary>
    void InfoFormat(string format, object arg1);
    /// <summary>Add a formated info(-rmational) message to the log.</summary>
    void InfoFormat(string format, object arg1, object arg2);
    /// <summary>Add a formated info(-rmational) message to the log.</summary>
    void InfoFormat(string format, params object[] args);

    /// <summary>Add a detail message to the log.</summary>
    void Detail(string message);

    /// <summary>Add a formated detail message to the log.</summary>
    void DetailFormat(string format, object arg1);
    /// <summary>Add a formated detail message to the log.</summary>
    void DetailFormat(string format, object arg1, object arg2);
    /// <summary>Add a formated detail message to the log.</summary>
    void DetailFormat(string format, params object[] args);

    /// <summary>The log into which messages getting added.</summary>
    ILog Log { get; }

  }

  /// <summary>A log containing message entries.</summary>
  public interface ILog {

    /// <summary>Start time of the log.</summary>
    DateTime StartAt { get; }

    /// <summary>Detail level of the log.</summary>
    JobLogLevel Level { get; }

    /// <summary>Test if a given level matches the level of the log.</summary>
    bool IsLogLevel(JobLogLevel level);

    /// <summary>Returns true if the log contains problem entries.</summary>
    bool HasProblem { get; }

    /// <summary>The local time of the <c>logEntry</c>.</summary>
    DateTime EntryTime(ILogEntry logEntry);

    /// <summary>The entries of the log.</summary>
    IEnumerable<ILogEntry> Entries { get; }
  }


  /// <summary>Representation of an entry in the log.</summary>
  public interface ILogEntry {

    /// <summary>Elapsed time in msec from start of the log to when the entry was generated.</summary>
    int ElapsedMsec { get; }

    /// <summary>Severity level of the entry.</summary>
    JobLogLevel Level { get; }

    /// <summary>Processing step name.</summary>
    string ProcessStep { get; }

    /// <summary>Entry message.</summary>
    string Message { get; }

  }



}
