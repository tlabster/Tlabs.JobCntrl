using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Tlabs.Test.Common {
  public class AppTimeEnvironment : IDisposable {
    public AppTimeEnvironment() {
      App.TimeInfo= new DateTimeHelper(TimeZoneInfo.Local);

      var svcColl= new ServiceCollection().AddLogging(log => log.AddConsole());
      Tlabs.App.ServiceProv= svcColl.BuildServiceProvider();
    }
    public void Dispose() {}
  }
}