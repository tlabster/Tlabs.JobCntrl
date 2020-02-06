using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Tlabs.Test.Common {
  public class SvcProvEnvironment : IDisposable {
    public SvcProvEnvironment() {
      var svcColl= new ServiceCollection().AddLogging(log => log.AddConsole());
      Tlabs.App.ServiceProv= svcColl.BuildServiceProvider();
    }
    public void Dispose() {}
  }
}