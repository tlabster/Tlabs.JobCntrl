using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using Xunit;
using Moq;

using Tlabs.Misc;
using Tlabs.Test.Common;
using Tlabs.JobCntrl.Test;

namespace Tlabs.JobCntrl.Model.Intern.Starter.Test {

  [Collection("AppTimeScope")]
  public class FileStarterTest {
    SvcProvEnvironment appTimeEnv;
    IJobControl jobCntrlRuntime;
    RTTestStarter rtStarter;

    public FileStarterTest(SvcProvEnvironment appTimeEnv) {
      this.appTimeEnv= appTimeEnv;

      var jcntrlMock= new Mock<IJobControl>();
      this.rtStarter= new RTTestStarter();
      rtStarter.Initialize("rtTimedStarter", "test description", null);
      jcntrlMock.Setup(j => j.Starters).Returns(new Dictionary<string, IStarter> {[rtStarter.Name]= rtStarter});
      this.jobCntrlRuntime= jcntrlMock.Object;
    }

    [Fact]
    public void ThrowTest() {
      var fileStarter= new Starter.FileSystemWatcher();
      Assert.ThrowsAny<JobCntrlConfigException>(() => fileStarter.Initialize("timedStarter", "test description", new Dictionary<string, object> {
        [FileSystemWatcher.PROP_DIR_PATH]= "non-exisitng"
      }));
    }

    [Fact]
    public async Task BasicTest() {
      const string fn= "test.file";
      var fnPath= Path.Combine(Path.GetDirectoryName(Tlabs.App.MainEntryPath), fn);
      File.Delete(fnPath);
      using var fileStarter= new FileSystemWatcher();
      fileStarter.Initialize("timedStarter", "test description", new Dictionary<string, object> {
        [FileSystemWatcher.PROP_FILE_NAME]= fn
      });
      var tcs= new TaskCompletionSource();
      var actCnt= 0;
      fileStarter.Activate+= (starter, props)=> {
        ++actCnt;
        fileStarter.Enabled= false;
        tcs.TrySetResult();
        return false;
      };
      fileStarter.Enabled= true;
      File.WriteAllText(fnPath, "test");
      await tcs.Task.Timeout(2000);
      Assert.Equal(1, actCnt);
    }

  }

}
