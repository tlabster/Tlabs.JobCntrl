using System.IO;

namespace Tlabs.JobCntrl.Model.Intern.Starter {
  
  /// <summary>Starter that activates on file system changes.</summary>
  /// <remarks>
  /// <para>The starter is watching for any changes to files in a file system directory specified by the <c>PROP_DIR_PATH</c> config. property.
  /// The property <c>PROP_FILE_NAME</c> can be used to narrow the files to be monitored in the directory. If empty 
  /// (or not specified) all files are monitored. Specify a complete file name or any wild card pattern like 
  /// '*.csv', 'data-*.xml', 'pos_???_record.*' to filter the files to be monitored for changes.</para>
  /// <para>The actual changed file causing the starter to activate is set with it's full file path in the
  /// activation/run property <c>RPROP_FILE_PATH</c></para>
  /// </remarks>
  public class FileSystemWatcher : BaseStarter {
    /// <summary>Property name that specifies the path of the directory to monitore for changes.</summary>
    public const string PROP_DIR_PATH= "Directory-Path";
    /// <summary>Property name that specifies the file name or file pattern to monitor.</summary>
    public const string PROP_FILE_NAME= "File-Name";
    /// <summary>Run-property name of the property set on job activation that specifies the full path of the detected file.</summary>
    public const string RPROP_FILE_PATH= "Detected-File-Path";

    private System.IO.FileSystemWatcher fileWatcher;

    /// <summary>Internal starter initialization.</summary>
    protected override IStarter InternalInit() {
      var directoryPath= PropertyString(PROP_DIR_PATH);
      if (!Path.IsPathRooted(directoryPath))
        directoryPath= Path.Combine(Path.GetDirectoryName(Tlabs.App.MainEntryPath), directoryPath);
      
      var watchDir= new DirectoryInfo(directoryPath);
      if (false == watchDir.Exists) throw new JobCntrlConfigException($"Directory does not exist: '{watchDir.FullName}'");

      var fileName= PropertyString(PROP_FILE_NAME, "").Trim();

      this.fileWatcher= new System.IO.FileSystemWatcher(watchDir.FullName, fileName) {
        NotifyFilter= NotifyFilters.LastWrite
      };
      this.fileWatcher.Changed+= FileWatcherEventHandler;
      ChangeEnabledState(this.isEnabled);
      return this;
    }

    /// <summary>Changes the enabled state of the starter according to <paramref name="enabled"/>.</summary>
    /// 
    [System.Security.SecurityCritical]
    protected override void ChangeEnabledState(bool enabled) {
      this.isEnabled= enabled;
      var fwatch= this.fileWatcher;
      if (null !=fwatch) fwatch.EnableRaisingEvents= this.isEnabled;
    }

    private void FileWatcherEventHandler(object sender, FileSystemEventArgs fsArgs) {
      if (WatcherChangeTypes.Changed != fsArgs.ChangeType) return;
      /* Note: When overwriting an existing file, this typically results into two operations:
       *    1. truncate file
       *    2. write new contents
       * Both operations are reported by FileSystemWatcher with WatcherChangeTypes.Changed.
       * Since we only want to activate when the new file has been entirely written, we filter out the truncate by
       * testing for the file length.
       * Caution: This also means that we in general do not activate for files with zero length!!!
       */
      if (0 == new FileInfo(fsArgs.FullPath).Length) return;

      var runProps = new ConfigProperties {
        [RPROP_FILE_PATH]= fsArgs.FullPath
      };
      this.DoActivate(runProps);
    }


    /// <summary>Dispose managed resources on <paramref name="disposing"/> == true.</summary>
    protected override void Dispose(bool disposing) {
      base.Dispose(disposing);

      var fwatch= fileWatcher;
      if (disposing && null != fwatch) fwatch.Dispose();
#pragma warning disable //help gc
      fileWatcher= fwatch= null;
#pragma warning restore
    }
  }
}
