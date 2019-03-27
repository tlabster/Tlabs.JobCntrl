using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;

using Tlabs.Misc;
using Tlabs.Data.Serialize.Json;

namespace Tlabs.JobCntrl.Intern {

  class StarterCompletionJsonPersister : IStarterCompletionPersister {
    /// <summary>Default <see cref="App.ContentRoot"/> relative persistence path.</summary>
    public const string DEFAULT_PERSISTENCE_PATH= "data/jobStartLog";
    static readonly Encoding INFO_ENCODING= Encoding.UTF8;
    const string CONTENT_TYPE= "text/json";
    private DirectoryInfo complLogDir;
    private object synchLog= new object();

    /// <summary>Default Ctor.</summary>
    public StarterCompletionJsonPersister() : this(DEFAULT_PERSISTENCE_PATH) { }

    /// <summary>Ctor from <paramref name="persistencePath"/>.</summary>
    public StarterCompletionJsonPersister(string persistencePath) {
      var complLogPath= Path.Combine(App.ContentRoot, persistencePath);
      this.complLogDir= new DirectoryInfo(complLogPath);
      this.complLogDir.Create();
      this.complLogDir.Refresh();
    }

    /// <summary>Event fired when a starter completion info has been persisted.</summary>
    public event Action<IStarterCompletionPersister, Model.Intern.IStarterCompletion, object> CompletionInfoPersisted;

    public Stream GetLastCompletionInfo(string starterName, out string contentType, out Encoding infoEncoding) {
      lock (synchLog) {
        contentType= CONTENT_TYPE;
        infoEncoding= INFO_ENCODING;
        var logFile= BuildFileInfo(starterName);
        return logFile.Exists
               ? new FileStream(logFile.FullName, FileMode.Open, FileAccess.Read)
               : null;
      }
    }

    public void StoreCompletionInfo(Model.Intern.IStarterCompletion starterCompletion) {
      FileInfo logFile;
      lock (synchLog) logFile= SerializeStarterCompletion(starterCompletion);

      CompletionInfoPersisted?.Invoke(this, starterCompletion, logFile);
    }

    private FileInfo BuildFileInfo(string starterName) {
      return new FileInfo(Path.Combine(complLogDir.FullName, starterName) + ".json");
    }

    private FileInfo SerializeStarterCompletion(Model.Intern.IStarterCompletion starterCompletion) {
      var logFile= BuildFileInfo(starterCompletion.StarterName);
      var json= JsonFormat.CreateDynSerializer();

      json.WriteObj(new FileStream(logFile.FullName, FileMode.Create), buildStarterCompletion(starterCompletion));

      // FileStream fstrm= null;
      // try {
      //   using (var strm = new StreamWriter(fstrm= new FileStream(logFile.FullName, FileMode.Create), Encoding.UTF8)) {
      //     fstrm= null;
      //     strm.WriteLine('{');

      //     strm.Write("Starter: "); JsonString.Write(strm, starterCompletion.StarterName).WriteLine(',');
      //     strm.Write("Time: "); JsonString.Write(strm, starterCompletion.Time).WriteLine(',');
      //     strm.WriteLine("JobResults: [");
      //     bool first= true;
      //     foreach (var agRes in starterCompletion.JobResults) {
      //       if (!first) strm.WriteLine(',');
      //       first= false;
      //       SerializeJobResult(strm, agRes);
      //     }
      //     strm.WriteLine();

      //     strm.WriteLine(']');  //JobResults
      //     strm.WriteLine('}');
      //   }
      // }
      // finally { if (null != fstrm) fstrm.Dispose(); }
      return logFile;
    }

    private object buildStarterCompletion(Model.Intern.IStarterCompletion starterCompletion) {
      return new {
        starter= starterCompletion.StarterName,
        time= starterCompletion.Time,
        jobResults= this.buildJobResults(starterCompletion.JobResults)
      };
    }

    private IEnumerable buildJobResults(IEnumerable<Model.IJobResult> jobResults) {
      foreach (var jbRes in jobResults) {
        yield return new {
          job= jbRes.JobName,
          endTime= jbRes.EndAt,
          success= jbRes.IsSuccessful,
          message= jbRes.Message,
          log=   jbRes.ProcessingLog == null
               ? null
               : new {
                 hasProblem= jbRes.ProcessingLog.HasProblem,
                 entries= buildLogEntries(jbRes.ProcessingLog, jbRes.ProcessingLog.Entries)
               }
        };
      }
    }

    private IEnumerable buildLogEntries(Model.ILog log, IEnumerable<Model.ILogEntry> entries) {
      foreach (var ent in entries) {
        yield return new {
          lev= ent.Level.ToString(),
          time= string.Format("{0:HH:mm:ss,FFF}", log.EntryTime(ent)),
          step= ent.ProcessStep,
          msg= ent.Message
        };
      }
    }

    // private static void SerializeJobResult(TextWriter strm, Model.IJobResult jobRes) {
    //   strm.WriteLine();
    //   strm.WriteLine('{');

    //   strm.Write("Job: "); JsonString.Write(strm, jobRes.JobName).WriteLine(',');
    //   strm.Write("EndTime: "); JsonString.Write(strm, jobRes.EndAt).WriteLine(',');
    //   strm.Write("Successful: "); strm.WriteLine(jobRes.IsSuccessful ? "true," : "false,");
    //   strm.Write("Message: "); JsonString.Write(strm, jobRes.Message);

    //   var log= jobRes.ProcessingLog;
    //   if (null != log) {
    //     strm.WriteLine(',');
    //     strm.WriteLine("Log: {");
    //     strm.Write("HasProblem: "); strm.WriteLine(log.HasProblem ? "true," : "false,");
    //     strm.WriteLine("Entries: [");
    //     bool first= true;
    //     foreach (var entry in log.Entries) {
    //       if (!first) strm.WriteLine(',');
    //       first= false;
    //       strm.Write("{{Lev: {0}, Time: \"{1:HH:mm:ss,FFF}\", Step: {2}, Msg: {3}}}",
    //         JsonString.Encoded(entry.Level.ToString()),
    //         log.EntryTime(entry),
    //         JsonString.Encoded(entry.ProcessStep),
    //         JsonString.Encoded(entry.Message)
    //       );
    //     }
    //     strm.WriteLine();

    //     strm.WriteLine("]}");  // Log/Entries
    //   }
    //   strm.Write('}');
    // }

  }//class
}
