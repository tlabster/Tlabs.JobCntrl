using System;
using System.Collections.Generic;
using System.Globalization;

namespace Tlabs.JobCntrl.Model.Intern.Starter {
  
  
  sealed class TimeSchedule : BaseStarter {
    public const string PARAM_SCHEDULE_TIME= "schedule-Time";
    private static readonly Timing.TimeScheduler timer= new Timing.TimeScheduler();

    private Timing.ITimePlan timePlan;
    private Action dueTimeHandler;

    protected override IStarter InternalInit() {
      var schedTimeStr= PropertyString(PARAM_SCHEDULE_TIME);
      if (string.IsNullOrEmpty(schedTimeStr)) throw new JobCntrlConfigException("Config. property '" + PARAM_SCHEDULE_TIME + "' not specified");
      schedTimeStr= schedTimeStr.Replace((char)0x0A0, ' '); //non breaking space U+00A0 has been seen once !!!

      timePlan= new Timing.ScheduleTime(schedTimeStr);
      dueTimeHandler= () => this.DoActivate(null);
      
      return this;
    }

    protected override void ChangeEnabledState(bool enabled) {
      if (true == (isEnabled= enabled))
        timer.Add(timePlan, dueTimeHandler);
      else
        timer.Remove(dueTimeHandler);
    }

    protected override void Dispose(bool disposing) {
      base.Dispose(disposing);
      if (disposing)
        timer.Dispose();
    }
  }

}
