using System;
using System.IO;
using System.Collections.Generic;

using Xunit;
using Xunit.Abstractions;

using Tlabs.JobCntrl.Intern;
using Tlabs.JobCntrl.Model.Intern;
using Tlabs.JobCntrl.Model;

namespace Tlabs.JobCntrl.Test {

  public class ComplJsonPersisterTest {

    [Fact]
    public void PersistenceTest() {
      var jsonPers= new StarterCompletionJsonPersister();
      var compl= new StarterCompl();
      jsonPers.CompletionInfoPersisted+= (pers, stCompl, obj) => {
        Assert.Same(jsonPers, pers);
        Assert.Same(compl, stCompl);
        Assert.NotNull(obj);
        Assert.True(File.Exists(obj.ToString()));
      };
      jsonPers.StoreCompletionInfo(compl);
    }

    class StarterCompl : IStarterCompletion {
      DateTime time= DateTime.Now;
      public string StarterName => "Test-Starter";

      public DateTime Time => time;

      public IReadOnlyDictionary<string, object> RunProperties => new Dictionary<string, object> {
        ["prop1"]= "prop1-test",
        ["numProp2"]= 123,
        ["dateProp3"]= DateTime.Now
      };

      public IEnumerable<IJobResult> JobResults => new JobResult[] {
        new JobResult("Test-Job001", true, logger(log => {
          log.ProcessStep= "TST";
          log.Detail("some test log");
          log.ProcessStep= "END";
          log.Info("log end");
        })),
        new JobResult("Test-Job002", "testing message", logger(log => {
          log.Info("test end");
        })),
        new JobResult("Test-Job003",
        new Dictionary<string, object> {
          ["res1"]= "res1-test",
          ["numRes2"]= 123,
          ["dateRes3"]= DateTime.Now
        },
        "result message")
      };

      private ILog logger(Action<IJobLogger> log) {
        var logger= new JobLogger(JobLogLevel.Detail);
        log(logger);
        return logger.Log;
      }
    }
  }
}