using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FolderSyncLib
{
    /// <summary>
    /// Sets the mode for copying files.
    /// <para>Copy - Only copies files to the new location</para>
    /// <para>CopyAndDelete - Additionally deletes files from the new location if they're not in the source location</para>
    /// </summary>
    public enum SyncMode
    {
        Copy, CopyAndDelete
    }
    public class FolderSync
    {
        public delegate void DirectorySyncing(string path);
        /// <summary>
        /// Fires when a new folder starts to sync.
        /// </summary>
        public event DirectorySyncing DirectorySync;
        public delegate void FileSyncing(string path);
        /// <summary>
        /// Fires when a new file starts to sync.
        /// </summary>
        public event FileSyncing FileSync;
        public delegate void Error(string msg);
        /// <summary>
        /// Fires when an error while copying the file occurs.
        /// </summary>
        public event Error OnError;
        public delegate void Finished();
        /// <summary>
        /// Fires once the synchronization is done.
        /// </summary>
        public event Finished OnFinished;



        private string excludedPathsFilePath = "excluded_paths.cfg";
        public string Name { get; set; }
        public string SourcePath { get; private set; }
        public string DestinationPath { get; private set; }
        public SyncMode SyncMode { get; private set; }
        public List<string> ExcludedPaths { get; private set; }


        /// <summary>
        /// Creates a new instance of the FolderSync.
        /// </summary>
        /// <param name="source">Source path</param>
        /// <param name="destination">Destination path</param>
        public FolderSync(string name,string source, string destination, SyncMode mode)
        {
            ExcludedPaths = new List<string>();
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

            if (!File.Exists(excludedPathsFilePath))
                File.Create(excludedPathsFilePath);
            else
                ExcludedPaths = File.ReadAllLines(excludedPathsFilePath).Where(x => !x.StartsWith(";")).ToList();
        }

        /// <summary>
        /// Starts the synchronization.
        /// </summary>
        public void StartSync()
        {
            SyncFilesystem(SourcePath, DestinationPath);
            OnFinished?.Invoke();
        }

        private bool cancel;
        public void CancelSync()
        {
            cancel = true;
        }

        private void SyncFilesystem(string sourcePath, string destPath)
        {
            if (cancel)
                return;
            foreach (var file in Directory.GetFiles(sourcePath).Where(x=>!ExcludedPaths.Contains(x)))
            {
                try
                {
                    FileSync?.Invoke(file);

                    //string newDestFilePath = Path.Combine(destPath, Path.GetFileName(file));
                    string newDestFilePath = file.Replace(sourcePath, destPath);

                    if (!File.Exists(newDestFilePath))
                        File.Copy(file, newDestFilePath);
                    else if (new FileInfo(file).LastWriteTimeUtc != new FileInfo(newDestFilePath).LastWriteTimeUtc)
                        File.Copy(file, newDestFilePath, true);

                }
                catch (Exception e)
                {
                    SomeError(file + " " + e.Message);
                }
            }

            //Removes all files only present in the destination location
            if (SyncMode == SyncMode.CopyAndDelete)
                foreach (var dir in Directory.GetFiles(destPath)
                    .Select(x => Path.GetFileName(x))
                    .Except(Directory.GetFiles(sourcePath)
                        .Select(x => Path.GetFileName(x))))
                    File.Delete(Path.Combine(destPath,dir));

            foreach (var dir in Directory.GetDirectories(sourcePath).Where(x => !ExcludedPaths.Contains(x)))
            {
                try
                {
                    DirectorySync?.Invoke(dir);

                    //string newDestPath = Path.Combine(destPath, Path.GetFileName(dir));
                    string newDestPath = dir.Replace(sourcePath, destPath);

                    if (!Directory.Exists(newDestPath))
                        Directory.CreateDirectory(newDestPath);
                    
                    SyncFilesystem(dir, newDestPath);
                }
                catch (Exception e)
                {
                    SomeError(dir + " " + e.Message);
                }
            }
            //Removes all directories only present in the destination location
            if (SyncMode == SyncMode.CopyAndDelete)
                foreach (var dir in Directory.GetDirectories(destPath)
                    .Select(x => Path.GetFileName(x))
                    .Except(Directory.GetDirectories(sourcePath)
                        .Select(x => Path.GetFileName(x))))
                    Directory.Delete(Path.Combine(destPath, dir), true);
        }

        private void SomeError(string msg)
        {
            OnError?.Invoke(msg);
            WriteLog(msg + "\n");
        }

        private void WriteLog(string msg)
        {
            File.AppendAllText("log.txt", "[" + DateTime.Now.ToLongTimeString() + "] " + msg);
        }

        /// <summary>
        /// Excludes path which should not be synced.
        /// </summary>
        /// <param name="paths">Paths</param>
        public void ExcludePaths(params string[] paths)
        {
            foreach (var path in paths)
            {
                AddExcludedFiles(path);
            }
        }
        /// <summary>
        /// Excludes path which should not be synced.
        /// </summary>
        /// <param name="paths">Path to file containing paths in a newline separated list.</param>
        public void ExcludePaths(string filePath)
        {
            if (File.Exists(filePath))
            {
                ExcludePaths(File.ReadAllLines(filePath));
            }
        }

        /// <summary>
        /// Removes all excluded paths.
        /// </summary>
        public void RemoveExcludedPaths()
        {
            ExcludedPaths.Clear();
            AddExcludedFiles();
        }

        /// <summary>
        /// Removes the given excluded paths.
        /// </summary>
        /// <param name="paths"></param>
        public void RemoveExcludedPaths(params string[] paths)
        {
            foreach (var path in paths)
            {
                if (ExcludedPaths.Contains(path))
                    ExcludedPaths.Remove(path);
            }
            AddExcludedFiles();
        }

        private void AddExcludedFiles(string path = "")
        {
            if (path != "" && !ExcludedPaths.Contains(path))
                ExcludedPaths.Add(path);

            File.WriteAllLines(excludedPathsFilePath, ExcludedPaths);
        }
    }
}
