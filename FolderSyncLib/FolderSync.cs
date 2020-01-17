using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FolderSyncLib
{
    /// <summary>
    /// Sets the mode for copying files.
    /// <para>Copy - Only copies files to the new location</para>
    /// <para>CopyAndDelete - Additionally deletes files from the new location if they're not in the source location</para>
    /// <para>CopyWithVersioning - Like copy but doesn't overwrite files, instead saves the old one with a new name</para>
    /// </summary>
    public enum SyncMode
    {
        Copy, CopyAndDelete, CopyWithVersioning
    }

    public enum LogLevel
    {
        None = 0, Error = 1<<0, Directory = 1<<1, File = 1<<2, Finished = 1<<3, FileDeleted = 1<<4, DirectoryDeleted = 1<<5
    }
    public class FolderSync
    {
        public delegate void Logging(FolderSync sender, LogLevel logLevel, string msg);
        public event Logging OnLog;

        public string Name { get; set; }
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }
        public SyncMode SyncMode { get; set; }
        public List<string> Ignore { get; private set; }
        /// <summary>
        /// Dateformat when CopyWithVerisoning is active. Default: 'yyyyMMddhhmmss'
        /// </summary>
        public string Dateformat { get; set; }
        /// <summary>
        /// Default: 9 (Error, Finished)
        /// </summary>
        public LogLevel LogLevel { get; set; }

        /// <summary>
        /// Creates a new instance of the FolderSync.
        /// </summary>
        /// <param name="name">Same name will load the same config with excluded paths</param>
        /// <param name="source">Source path</param>
        /// <param name="destination">Destination path</param>
        /// <param name="mode">How shall the files be copied</param>
        /// <param name="logLevel">Which information shall be written to a log file</param>
        public FolderSync(string name, string source, string destination, SyncMode mode, LogLevel logLevel = LogLevel.Error | LogLevel.Finished)
        {

            cancel = false;

            if (destination.Contains(source))
                throw new Exception("Destination path can not be located in source path.");

            if (!Directory.Exists(source))
                throw new Exception("Source location does not exist");

            if (!Directory.Exists(destination))
                Directory.CreateDirectory(destination);

            Name = name;
            SourcePath = source;
            DestinationPath = destination;
            SyncMode = mode;
            this.LogLevel = logLevel;
            Ignore = new List<string>();
            Dateformat = "yyyyMMddhhmmss";
            WriteLogToFile = true;
        }

        /// <summary>
        /// Starts the synchronization.
        /// </summary>
        public void Start()
        {
            SyncFilesystem(SourcePath, DestinationPath);
            if (GetBinary(LogLevel, 3))
                OnLog?.Invoke(this, LogLevel.Finished, "Sync finished");
        }

        /// <summary>
        /// Starts the synchronization asynchronous. 
        /// </summary>
        public Task StartAsync()
        {
            Task sync = new Task(() => SyncFilesystem(SourcePath, DestinationPath));
            sync.Start();

            return sync;
        }

        private bool cancel;
        /// <summary>
        /// Stops the sync 
        /// </summary>
        public void CancelSync()
        {
            cancel = true;
        }

        /// <summary>
        /// Actual method syncing all files
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destPath"></param>
        private void SyncFilesystem(string sourcePath, string destPath)
        {
            if (cancel)
                return;
            foreach (var file in Directory.GetFiles(sourcePath).Where(x => !Ignore.Any(y => Regex.IsMatch(x, y))))
            {
                if (cancel)
                    return;
                try
                {
                    if (GetBinary(LogLevel, 2))
                        OnLog?.Invoke(this, LogLevel.File, file);

                    string newDestFilePath = Path.Combine(destPath, Path.GetFileName(file));
                    //string newDestFilePath = file.Replace(sourcePath, destPath);

                    if (!File.Exists(newDestFilePath))
                        File.Copy(file, newDestFilePath);
                    else if (new FileInfo(file).LastWriteTimeUtc != new FileInfo(newDestFilePath).LastWriteTimeUtc)
                    {
                        if (SyncMode == SyncMode.CopyWithVersioning)
                        {
                            string fileName = Path.GetFileName(newDestFilePath);
                            string newPath = Path.GetFileNameWithoutExtension(fileName)
                                + "_"
                                + new FileInfo(newDestFilePath).LastWriteTimeUtc.ToString(Dateformat)
                                + Path.GetExtension(fileName);
                            File.Move(newDestFilePath, newPath);
                        }

                        File.Copy(file, newDestFilePath, true);
                    }
                }
                catch (Exception e)
                {
                    if (GetBinary(LogLevel, 0))
                        OnLog?.Invoke(this, LogLevel.Error, file + " " + e.Message);
                }
            }

            //Removes all files only present in the destination location
            if (SyncMode == SyncMode.CopyAndDelete)
                foreach (var file in Directory.GetFiles(destPath)
                    .Select(x => Path.GetFileName(x))
                    .Except(Directory.GetFiles(sourcePath)
                        .Select(x => Path.GetFileName(x))))
                {
                    File.Delete(Path.Combine(destPath, file));
                    if (GetBinary(LogLevel, 4))
                        OnLog?.Invoke(this, LogLevel.FileDeleted, Path.Combine(destPath, file));
                }

            foreach (var dir in Directory.GetDirectories(sourcePath).Where(x => !Ignore.Any(y => Regex.IsMatch(x, y))))
            {
                try
                {
                    if (GetBinary(LogLevel, 1))
                        OnLog?.Invoke(this, LogLevel.Directory, dir);

                    string newDestPath = Path.Combine(destPath, Path.GetFileName(dir));
                    //string newDestPath = dir.Replace(sourcePath, destPath);

                    if (!Directory.Exists(newDestPath))
                        Directory.CreateDirectory(newDestPath);

                    SyncFilesystem(dir, newDestPath);
                }
                catch (Exception e)
                {
                    if (GetBinary(LogLevel, 0))
                        OnLog?.Invoke(this, LogLevel.Error, dir + " " + e.Message);
                }
            }

            //Removes all directories only present in the destination location
            if (SyncMode == SyncMode.CopyAndDelete)
                foreach (var dir in Directory.GetDirectories(destPath)
                    .Select(x => Path.GetFileName(x))
                    .Except(Directory.GetDirectories(sourcePath)
                        .Select(x => Path.GetFileName(x))))
                {
                    Directory.Delete(Path.Combine(destPath, dir), true);
                    if (GetBinary(LogLevel, 5))
                        OnLog?.Invoke(this, LogLevel.DirectoryDeleted, Path.Combine(destPath, dir));
                }
        }

        /// <summary>
        /// Conditions for files/fodlers which should not be synced.
        /// </summary>
        /// <param name="paths">Paths</param>
        public void AddIgnore(params string[] regexes)
        {
            foreach (var regex in regexes)
                if (regex != "" && !Ignore.Contains(regex))
                    Ignore.Add(regex);
        }
        /// <summary>
        /// Conditions for files/fodlers which should not be synced.
        /// </summary>
        /// <param name="paths">Path to file containing regexes in a newline separated list.</param>
        public void AddIgnore(string filePath)
        {
            if (File.Exists(filePath))
                AddIgnore(File.ReadAllLines(filePath).Where(x => !x.StartsWith(";") && !x.StartsWith("#")).ToArray());
        }

        /// <summary>
        /// Removes all regexes.
        /// </summary>
        public void RemoveIgnore()
        {
            Ignore.Clear();
        }

        /// <summary>
        /// Removes the given regexes.
        /// </summary>
        /// <param name="paths"></param>
        public void RemoveIgnore(params string[] regexes)
        {
            foreach (var regex in regexes)
                if (Ignore.Contains(regex))
                    Ignore.Remove(regex);
        }

        /// <summary>
        /// Gets the binary value of a number at a certain position as a boolean.
        /// </summary>
        /// <param name="num"></param>
        /// <param name="position">Starting at 0</param>
        /// <returns></returns>
        private bool GetBinary(int num, int position)
        {
            return string.Join("", Convert.ToString(num, 2).Reverse())[position] == '1' ? true : false;
        }
        /// <summary>
        /// Gets the binary value of the log level at a certain position as a boolean.
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="position">Starting at 0</param>
        /// <returns></returns>
        private bool GetBinary(LogLevel mode, int position)
        {
            return GetBinary((int)mode, position);
        }
    }
}
