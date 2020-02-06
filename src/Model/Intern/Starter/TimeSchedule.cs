using System;
using System.Collections.Generic;
using System.Globalization;

namespace Tlabs.JobCntrl.Model.Intern.Starter {


  /// <summary>Starter to activate on time schedule.</summary>
  ///<remarks><see cref="Timing.ScheduleTime"/> for currently suported time pattern syntax.</remarks>
  public sealed class TimeSchedule : BaseStarter {
    /// <summary>Param. name of schedule time (pattern)</summary>
    public const string PARAM_SCHEDULE_TIME= "schedule-Time";
    private static readonly Timing.TimeScheduler timer= new Timing.TimeScheduler();

    private Timing.ITimePlan timePlan;
    private Action dueTimeHandler;

    ///<inheritdoc/>
    protected override IStarter InternalInit() {
      var schedTimeStr= PropertyString(PARAM_SCHEDULE_TIME);
      if (string.IsNullOrEmpty(schedTimeStr)) throw new JobCntrlConfigException("Config. property '" + PARAM_SCHEDULE_TIME + "' not specified");
      schedTimeStr= schedTimeStr.Replace((char)0x0A0, ' '); //non breaking space U+00A0 has been seen once !!!

      timePlan= new Timing.ScheduleTime(schedTimeStr);
      dueTimeHandler= () => this.DoActivate(null);
      
      return this;
    }

    ///<inheritdoc/>
    protected override void ChangeEnabledState(bool enabled) {
      if (true == (isEnabled= enabled))
        timer.Add(timePlan, dueTimeHandler);
      else
        timer.Remove(dueTimeHandler);
    }

    ///<inheritdoc/>
    protected override void Dispose(bool disposing) {
      base.Dispose(disposing);
      if (disposing)
        timer.Dispose();
    }
  }

}
