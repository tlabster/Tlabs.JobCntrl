# Tlabs.JobCntrl

A framework for executing background jobs based on *starter* conditions.

## Library Overview

Some of the services `Tlabs.JobCntrl` provides:

* A job-control runtime to maintain a configuration based execution plan of background jobs
* Background job abstraction template
* Job *starter* abstraction
* Basic *starter* implementions  
	* time scheduled
	* chained (on completion of previous jobs)
	* file system watcher
	* message subscription
* Job execution logging
* Job configuration model

### .NET version dependency
*	`2.1.*` .NET 6
*	`2.2.*` .NET 8
