using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Tlabs.Misc;

using Xunit;

namespace Tlabs.Test.Common {
  public class SvcProvEnvironment : AbstractServiceProviderFactory {
    public SvcProvEnvironment() {
      this.svcColl.AddLogging(log => log.AddConsole());
      Assert.NotNull(this.SvcProv);   //make sure the IServiceProvider gets initialized...
    }
  }
}