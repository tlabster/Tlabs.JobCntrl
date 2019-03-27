using System;

namespace Tlabs.Test.Common {
  public class AppTimeEnvironment : IDisposable {
    public AppTimeEnvironment() {
      App.TimeInfo= new DateTimeHelper(TimeZoneInfo.Local);
    }
    public void Dispose() {}
  }
}