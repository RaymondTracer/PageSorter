﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PageSorter
{
    class Program
    {
        static string rootDirectory = AppDomain.CurrentDomain.BaseDirectory;

        static Stopwatch sw;
        static int cursorTop = 0;
        static long fileSize = 0;

        static string Version
        {
            get
            {
                string[] assemblyVersionSplit = Assembly.GetEntryAssembly().GetName().Version.ToString().Split('.');
                string temp = assemblyVersionSplit[0];

                if (Convert.ToInt16(assemblyVersionSplit[3]) > 0)
                {
                    temp = Assembly.GetEntryAssembly().GetName().Version.ToString();
                }
                else if (Convert.ToInt16(assemblyVersionSplit[2]) > 0)
                {
                    temp = $"{assemblyVersionSplit[0]}.{assemblyVersionSplit[1]}.{assemblyVersionSplit[2]}";
                }
                else if (Convert.ToInt16(assemblyVersionSplit[1]) > 0)
                {
                    temp = $"{assemblyVersionSplit[0]}.{assemblyVersionSplit[1]}";
                }

                return temp;
            }
        }

        static void Main(string[] args)
        {
            Console.Title = $"Page Sorter v{Version} by Raymond Tracer";

#if DEBUG
            Console.WriteLine("[DEBUG MODE ENABLED]");
            Console.WriteLine("Press any key to start...");
            Console.ReadKey();
#endif

            if (args.Length > 0 && args[0] != null)
            {
                rootDirectory = Path.GetFullPath(args[0]);
                Directory.CreateDirectory(rootDirectory);
            }

            DeleteDirectoryRecursively($"{rootDirectory}{Path.DirectorySeparatorChar}work");

            sw = new Stopwatch();
            using WebClient wc = new WebClient();

            Console.WriteLine("Getting project info.");
            JsonClasses.ProjectInfo projectInfo = JsonSerializer.Deserialize<JsonClasses.ProjectInfo>(wc.DownloadString("https://papermc.io/api/v2/projects/paper"));

            Console.WriteLine("Getting version info.");
            JsonClasses.VersionInfo versionInfo = JsonSerializer.Deserialize<JsonClasses.VersionInfo>(wc.DownloadString($"https://papermc.io/api/v2/projects/paper/versions/{projectInfo.versions[^1]}"));

            Console.WriteLine("Getting builds.");
            JsonClasses.VersionBuilds builds = JsonSerializer.Deserialize<JsonClasses.VersionBuilds>(wc.DownloadString($"https://papermc.io/api/v2/projects/paper/versions/{projectInfo.versions[^1]}/builds/{versionInfo.builds[^1]}"));

            string downloadURL = $"https://papermc.io/api/v2/projects/paper/versions/{projectInfo.versions[^1]}/builds/{versionInfo.builds[^1]}/downloads/{builds.downloads.application.name}";
            string downloadFilePath = $"{rootDirectory}{Path.DirectorySeparatorChar}{builds.downloads.application.name}";

            Console.WriteLine();
            Console.WriteLine($"Verison: {projectInfo.versions[^1]}");
            Console.WriteLine($"Build: {versionInfo.builds[^1]}");
            Console.WriteLine($"Filename: {builds.downloads.application.name}");

            Console.WriteLine();
            Console.WriteLine($"Downloading...");

            wc.OpenRead(downloadURL);
            fileSize = Convert.ToInt64(wc.ResponseHeaders["Content-Length"]);

            Console.WriteLine($"0%   [                    ]");
            Console.WriteLine($"0KB/{fileSize / 1000d}KB");
            Console.WriteLine($"0KB/s");
            cursorTop = Console.CursorTop;

            wc.DownloadProgressChanged += (sender, args) =>
            {
                updateDownloadProgress(args);
            };

            wc.DownloadFileCompleted += (sender, args) =>
            {
                sw.Stop();

                while (processingProgress) { }
                Console.SetCursorPosition(0, cursorTop);
                Console.WriteLine();
                Console.WriteLine($"Download Competed! Took {sw.Elapsed.TotalSeconds} seconds.");
                finishedDownloading = true;
            };

            sw = Stopwatch.StartNew();
            wc.DownloadFileAsync(new Uri(downloadURL), downloadFilePath);

            while (!finishedDownloading) { }

            string workDirectory = $"{rootDirectory}{Path.DirectorySeparatorChar}work";
            string cacheDirectory = $"{workDirectory}{Path.DirectorySeparatorChar}cache";

            Directory.CreateDirectory(workDirectory);
            File.Move(downloadFilePath, $"{workDirectory}{Path.DirectorySeparatorChar}{builds.downloads.application.name}");

            Console.WriteLine();
            Console.WriteLine("Running Paperclip.");
            Console.WriteLine();

            Process java = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "java",
                    Arguments = $"-jar {workDirectory}{Path.DirectorySeparatorChar}{builds.downloads.application.name}",
                    WorkingDirectory = workDirectory
                }
            };
            java.Start();
            java.WaitForExit();

            Console.WriteLine();
            Console.WriteLine("Moving PaperMC server jar.");

            if (FileInUse($@"{rootDirectory}\..\paperclip.jar"))
            {
                Console.WriteLine("Old server jar in use, waiting for file to be freed.");

                while (FileInUse($@"{rootDirectory}\..\paperclip.jar")) { }
            }

            foreach (string file in Directory.GetFiles(cacheDirectory))
            {
                if (file.Contains("patched"))
                {
                    File.Move(file, $@"{rootDirectory}\..\paperclip.jar", true);
                    break;
                }
            }

            Console.WriteLine("Done, press any key to exit.");
            Console.ReadKey();
        }

        volatile static bool finishedDownloading = false;

        volatile static bool processingProgress = false;
        static int progreesPercentage = -1;
        static long bytesRec = -1;

        static object _sync = new object();
        static void updateDownloadProgress(DownloadProgressChangedEventArgs args)
        {
            if (args.BytesReceived > bytesRec)
            {
                progreesPercentage = args.ProgressPercentage;
                bytesRec = args.BytesReceived;
            }

            lock (_sync)
            {
                processingProgress = true;
                string spaces = progreesPercentage < 10 ? "   " : progreesPercentage < 100 ? "  " : " ";

                int progFill = (int)Math.Floor(progreesPercentage / 5d);
                string progFillString = new string('-', progFill);

                int progEmpty = 20 - progFill;
                string progEmptyString = new string(' ', progEmpty);

                Console.SetCursorPosition(0, cursorTop - 3);
                Console.WriteLine($"{progreesPercentage}%{spaces}[{progFillString}{progEmptyString}]        ");
                Console.WriteLine($"{bytesRec / 1000d:N}KB/{fileSize / 1000d:N}KB        ");
                Console.WriteLine($"{bytesRec / 1000d / sw.Elapsed.TotalSeconds:N}KB/s        ");
                processingProgress = false;
            }
        }

        public static void DeleteDirectoryRecursively(string targetDir)
        {
            if (Directory.Exists(targetDir))
            {
                string[] files = Directory.GetFiles(targetDir);
                string[] dirs = Directory.GetDirectories(targetDir);

                foreach (string file in files)
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }

                foreach (string dir in dirs)
                {
                    DeleteDirectoryRecursively(dir);
                }

                Directory.Delete(targetDir, false);
            }
        }

        public static bool FileInUse(string targetFile)
        {
            try
            {
                using Stream stream = new FileStream(targetFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return false;
            }
            catch
            {
            }

            return true;
        }
    }
}
