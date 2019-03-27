using System;
using System.Collections;
using System.Text;
using Xunit;

using Tlabs.JobCntrl.Model;
using Tlabs.JobCntrl.Model.Intern;

namespace Tlabs.JobCntrl.Test {

  public class JobLogTests {

    [Fact]
    //[ExpectedException(typeof(InvalidOperationException))]
    public void BasicLogTest() {
      var logger= new JobLogger(JobLogLevel.Detail);
      Assert.Equal(JobLogLevel.Detail, logger.Log.Level);

      logger.Problem("Problem text");
      Assert.Equal(1, Count(logger.Log.Entries));

      logger.Info("Info text");
      Assert.Equal(2, Count(logger.Log.Entries));

      logger.Detail("Detail text");
      Assert.Equal(3, Count(logger.Log.Entries));
      Assert.True(logger.Log.HasProblem, "Log must have problem");

      foreach (var itm in logger.Log.Entries) {
        //Assert.Equal(logger.Log.Level, itm.Level, "must match level");
        Assert.Equal(JobLogger.DEFAULT_PROCSTEP, itm.ProcessStep);
      }

      logger.ProcessStep= "X";
      logger.Detail("new step");
      string step= null;
      foreach (var itm in logger.Log.Entries) step= itm.ProcessStep;
      Assert.Equal("X", step);

      BaseJob.DumpLog(logger.Log);
    }

    [Fact]
    public void DetailLogTest() {
      var logger= new JobLogger(JobLogLevel.Info);
      Assert.Equal(JobLogLevel.Info, logger.Log.Level);

      logger.Detail("Detail text");
      Assert.Equal(0, Count(logger.Log.Entries));

      logger= new JobLogger(JobLogLevel.Problem);
      Assert.Equal(JobLogLevel.Problem, logger.Log.Level);

      logger.Detail("Detail text");
      Assert.Equal(0, Count(logger.Log.Entries));
      logger.Info("Info text");
      Assert.Equal(0, Count(logger.Log.Entries));
    }


    [Fact]
    public void LogLimitTest() {
      int n= 5;
      var logger= new JobLogger(JobLogLevel.Detail, n);
      logger.Problem("Keep this problem");
      logger.Info("Info msg");

      for (var l= 2; l < n; ++l) logger.Detail("Detail");
      Assert.Equal(n, Count(logger.Log.Entries));
      
      /* Exceed the limit by logging another detail:
       * This must shrink the log by strip off any detail messages and reduce the log level by one.
       * Note: Since shrinking takes place before adding this one additional detail and the the log-level
       *       is reduced after adding, this one extra detail message still gets into the log....!
       */
      logger.Detail("Another detail");
      Assert.Equal(2+1, Count(logger.Log.Entries));
      logger.Detail("Another detail");
      Assert.Equal(2+1, Count(logger.Log.Entries));
      Assert.Equal(JobLogLevel.Info, logger.Log.Level);
      Assert.False(logger.Log.IsLogLevel(JobLogLevel.Detail));

      for (var l= Count(logger.Log.Entries); l < n; ++l)
        logger.Problem("More problems to keep.");
      logger.Problem("Extra more problems to keep.");
      Assert.Equal(n-2+1, Count(logger.Log.Entries));
      Assert.False(logger.Log.IsLogLevel(JobLogLevel.Info));

      logger.Problem("Extra more problems to keep.");
      logger.Problem("Extra more problems to keep.");
      logger.Problem("Extra more problems to keep.");
      Assert.Equal(n-1+3, Count(logger.Log.Entries));

    }

    public static int Count(IEnumerable en) {
      int cnt= 0;
      foreach(object o in en) ++cnt;
      return cnt;
    }

  }//class JobLogTests
}
