using System.Collections.Generic;

using Xunit;
using Xunit.Abstractions;

using Tlabs.JobCntrl.Config;

namespace Tlabs.JobCntrl.Test {

  public class JsonLoaderTest : IClassFixture<JsonLoaderTest.LoaderCtx> {

    LoaderCtx ctx;
    public JsonLoaderTest(LoaderCtx ctx) { this.ctx= ctx; }

    public class LoaderCtx {
      public readonly JsonJobCntrlCfgLoader Loader= new JsonJobCntrlCfgLoader(
        new JobCntrlCfgLoaderProperties(new Dictionary<string, string> {["path"]= "JobCntrlConfig.json"}),
        new IJobCntrlConfigurator[0]);
    }

    [Fact]
    public void BasicTest() {
      var loader= ctx.Loader;

      var masterCfg= loader.LoadMasterConfiguration();
      Assert.NotEmpty(masterCfg.Starters);
      Assert.Equal("MANUAL", masterCfg.Starters["MANUAL"].Name);

      Assert.NotEmpty(masterCfg.Jobs);
      Assert.Equal("TEST", masterCfg.Jobs["TEST"].Name);

      var cntrlCfg= loader.LoadRuntimeConfiguration(masterCfg);
      Assert.NotEmpty(cntrlCfg.Starters);
      Assert.NotEmpty(cntrlCfg.Jobs);
    }
  }
}