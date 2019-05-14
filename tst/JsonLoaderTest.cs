using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

using Tlabs.JobCntrl.Config;

namespace Tlabs.JobCntrl.Test {

  public class JsonLoaderTest : IClassFixture<JsonLoaderTest.LoaderCtx> {

    LoaderCtx ctx;
    public JsonLoaderTest(LoaderCtx ctx) { this.ctx= ctx; }

    public class LoaderCtx {
      public readonly JsonJobCntrlCfgLoader Loader;

      public LoaderCtx() {
        var tstConfigurator= new JobCntrlConfigurator();
        tstConfigurator.DefineMasterStarter(  //duplicate overriding definition
          name: "MESSAGE", description: "Message activated starter",
          type: typeof(Tlabs.JobCntrl.Model.Intern.Starter.MessageSubscription).AssemblyQualifiedName
        );
        tstConfigurator.DefineMasterJob(
          name: "TEST", description: "Test Job",
          type: typeof(Tlabs.JobCntrl.Test.Job.TestJob).AssemblyQualifiedName
        );
        tstConfigurator.DefineStarter(
          name: "Test-Msg", description: "Test message starter",
          master: "MESSAGE"
        );
        tstConfigurator.DefineJob(
          name: "Msg-Job", description: "Test message handler job",
          master: "TEST", starter: "Test-Msg"
        );

        this.Loader= new JsonJobCntrlCfgLoader(
        new JobCntrlCfgLoaderProperties(new Dictionary<string, string> { ["path"]= "JobCntrlConfig.json" }),
        new IJobCntrlConfigurator[] { tstConfigurator });
      }
    }

    [Fact]
    public void BasicTest() {
      var loader= ctx.Loader;

      var masterCfg= loader.LoadMasterConfiguration();
      Assert.Equal(4, masterCfg.Starters.Count);
      Assert.Equal("MANUAL", masterCfg.Starters["MANUAL"].Name);

      Assert.Equal(2, masterCfg.Jobs.Count);
      Assert.Equal("TEST", masterCfg.Jobs["TEST"].Name);

      var cntrlCfg= loader.LoadRuntimeConfiguration(masterCfg);
      Assert.Equal(4, cntrlCfg.Starters.ToList().Count);
      Assert.Equal(5, cntrlCfg.Jobs.ToList().Count);
    }
  }
}