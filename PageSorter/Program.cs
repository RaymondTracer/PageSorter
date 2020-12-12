using PageSorter.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Xml;

namespace PageSorter
{
    class Program
    {
        static string rootDirectory = AppDomain.CurrentDomain.BaseDirectory;

        static Stopwatch swProgram;
        static Stopwatch swDownload;
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

#if DEBUG
                temp += " [DEBUG]";
#endif

                return temp;
            }
        }

        static void Main(string[] args)
        {
            Console.Title = $"Page Sorter v{Version} by Raymond Tracer";

#if DEBUG
            Console.WriteLine("[DEBUG MODE ENABLED]");
            Console.WriteLine();

            Console.WriteLine($"Arguments: {string.Join(" ", args)}");
            Console.WriteLine($"Current LastVerison: {Settings.Default.LastVersion}");
            Console.WriteLine($"Current LastBuild: {Settings.Default.LastBuild}");

            Console.WriteLine();
            Console.Write("Press any key to start...");
            ConsoleKeyInfo consoleKeyInfo = Console.ReadKey();
            Console.Write("\n");

            if (consoleKeyInfo.Key == ConsoleKey.R)
            {
                Settings.Default.LastVersion = "";
                Settings.Default.LastBuild = 0;
                Settings.Default.Save();

                Console.WriteLine("Reset LastVersion and LastBuild.");
            }
#endif

            swProgram = Stopwatch.StartNew();

            LinkedList<string> argsList = new LinkedList<string>(args);
            while (argsList.Count > 0)
            {
                LinkedListNode<string> first = argsList.First;

                if (string.Equals(first.Value, "-RootDirectory", StringComparison.OrdinalIgnoreCase))
                {
                    LinkedListNode<string> next = first.Next;

                    rootDirectory = Path.GetFullPath(next.Value);
                    Console.WriteLine($"Root directory set to: {rootDirectory}");

                    Directory.CreateDirectory(rootDirectory);

                    argsList.Remove(next);
                }
                else if (string.Equals(first.Value, "-LastVersion", StringComparison.OrdinalIgnoreCase))
                {
                    LinkedListNode<string> next = first.Next;

                    Settings.Default.LastVersion = next.Value;
                    Settings.Default.Save();

                    Console.WriteLine($"Last verison set to: {Settings.Default.LastVersion}");

                    argsList.Remove(next);
                }
                else if (string.Equals(first.Value, "-LastBuild", StringComparison.OrdinalIgnoreCase))
                {
                    LinkedListNode<string> next = first.Next;

                    Settings.Default.LastBuild = Convert.ToInt32(next.Value);
                    Settings.Default.Save();

                    Console.WriteLine($"Last build set to: {Settings.Default.LastBuild}");

                    argsList.Remove(next);
                }
                else
                {
                    Console.WriteLine($"Bad argument: {first.Value}");
                }

                argsList.Remove(first);
            }

            // Probly best to initialize "global" local variables here.
            string workDirectory = $"{rootDirectory}{Path.DirectorySeparatorChar}work";
            string cacheDirectory = $"{workDirectory}{Path.DirectorySeparatorChar}cache";

            Console.WriteLine();
            Console.WriteLine($"Last Verison: {Settings.Default.LastVersion}");
            Console.WriteLine($"Last Build: {Settings.Default.LastBuild}");
            Console.WriteLine();

            using WebClient wc = new WebClient();

            Console.WriteLine("Getting latest version of Minecraft currently supported by PaperMC.");
            JsonClasses.ProjectInfo projectInfo = JsonSerializer.Deserialize<JsonClasses.ProjectInfo>(wc.DownloadString("https://papermc.io/api/v2/projects/paper"));
            CheckForError(projectInfo);
            Console.WriteLine($"Found Verison: {projectInfo.versions[^1]}");

            Console.WriteLine("Getting latest build number.");
            JsonClasses.VersionInfo versionInfo = JsonSerializer.Deserialize<JsonClasses.VersionInfo>(wc.DownloadString($"https://papermc.io/api/v2/projects/paper/versions/{projectInfo.versions[^1]}"));
            CheckForError(versionInfo);
            Console.WriteLine($"Found Build: {versionInfo.builds[^1]}");

            Console.WriteLine("Getting latest download filename.");
            JsonClasses.VersionBuilds builds = JsonSerializer.Deserialize<JsonClasses.VersionBuilds>(wc.DownloadString($"https://papermc.io/api/v2/projects/paper/versions/{projectInfo.versions[^1]}/builds/{versionInfo.builds[^1]}"));
            CheckForError(builds);
            Console.WriteLine($"Found Filename: {builds.downloads.application.name}");

            string downloadURL = $"https://papermc.io/api/v2/projects/paper/versions/{projectInfo.versions[^1]}/builds/{versionInfo.builds[^1]}/downloads/{builds.downloads.application.name}";
            string downloadFilePath = $"{rootDirectory}{Path.DirectorySeparatorChar}{builds.downloads.application.name}";

            string oldVerInfo = string.Empty;
            if (string.IsNullOrWhiteSpace(Settings.Default.LastVersion))
            {
                Settings.Default.LastVersion = projectInfo.versions[^1];
                Settings.Default.LastBuild = Math.Max(versionInfo.builds[0], versionInfo.builds[^1] - 5);
                Settings.Default.Save();
            }
            else if (projectInfo.versions[^1] != Settings.Default.LastVersion)
            {
                Settings.Default.LastVersion = projectInfo.versions[^1];
                Settings.Default.LastBuild = 0;
                Settings.Default.Save();

                oldVerInfo = $", Updated from {Settings.Default.LastVersion}";
            }

            int oldBuild = 0;
            if (Settings.Default.LastBuild == versionInfo.builds[^1])
            {
                Console.WriteLine();
                Console.WriteLine("You already have the latest version.");

                swProgram.Stop();
            RedownloadConfirmation:
                Console.Write("Do you want to redownload? (y/N): ");
                ConsoleKeyInfo keyInfo = Console.ReadKey();
                Console.Write("\n");

                if (keyInfo.Key == ConsoleKey.Enter || keyInfo.Key == ConsoleKey.N)
                {
                    Console.Write("Press any key to exit.");
                    Console.ReadKey(true);
                    Environment.Exit(0);

                }
                else if (keyInfo.Key != ConsoleKey.Y)
                {
                    Console.WriteLine($"Invalid input: {Enum.GetName(keyInfo.Key)}");
                    Console.WriteLine();

                    goto RedownloadConfirmation;
                }

                swProgram.Start();
                oldBuild = versionInfo.builds[^1];
            }
            else if (Settings.Default.LastBuild > versionInfo.builds[^1])
            {
                oldBuild = Math.Max(versionInfo.builds[0], versionInfo.builds[^1] - 5);
                Settings.Default.LastBuild = versionInfo.builds[^1];
                Settings.Default.Save();
            }
            else
            {
                oldBuild = Settings.Default.LastBuild;
                Settings.Default.LastBuild = versionInfo.builds[^1];
                Settings.Default.Save();
            }

            DeleteDirectoryRecursively(workDirectory);
            File.Delete($@"{rootDirectory}\changelog.txt");

            Console.WriteLine();
            Console.WriteLine($"Latest Verison: {projectInfo.versions[^1]}{oldVerInfo}");
            Console.WriteLine($"Latest Build: {versionInfo.builds[^1]}");
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
                UpdateDownloadProgress(args);
            };

            wc.DownloadFileCompleted += (sender, args) =>
            {
                swDownload.Stop();

                while (processingProgress) { }
                Console.SetCursorPosition(0, cursorTop);
                Console.WriteLine();
                Console.WriteLine($"Download Competed! Took {swDownload.Elapsed.TotalSeconds} seconds.");
                finishedDownloading = true;
            };

            swDownload = Stopwatch.StartNew();
            wc.DownloadFileAsync(new Uri(downloadURL), downloadFilePath);

            while (!finishedDownloading) { }

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
                Console.WriteLine("Old server jar in use, waiting for the file to be freed.");

                while (FileInUse($@"{rootDirectory}\..\paperclip.jar")) { }
            }

            Console.WriteLine();
            if (File.Exists($@"{cacheDirectory}\patched_{Settings.Default.LastVersion}.jar"))
            {
                File.Move($@"{cacheDirectory}\patched_{Settings.Default.LastVersion}.jar", $@"{rootDirectory}\..\paperclip.jar", true);

                if (Settings.Default.LastBuild - oldBuild < 1)
                {
                    oldBuild -= 5;
                }

                Console.WriteLine($"Getting changelog for builds {oldBuild} to {Settings.Default.LastBuild}");

                List<string> commits = new List<string>();
                Dictionary<int, LinkedList<string>> buildLines = new Dictionary<int, LinkedList<string>>();
                LinkedList<string> lines = new LinkedList<string>();

                for (int i = oldBuild - 5; i < oldBuild; i++)
                {
#if DEBUG
                    Console.WriteLine($"[- {i} -]");

                    int commitCount = 0;
                    int duplicateCommits = 0;
#endif
                    JsonClasses.VersionBuilds temp = JsonSerializer.Deserialize<JsonClasses.VersionBuilds>(wc.DownloadString($"https://papermc.io/api/v2/projects/paper/versions/{projectInfo.versions[^1]}/builds/{i}"));
                    foreach (JsonClasses.Change change in temp.changes)
                    {
#if DEBUG
                        commitCount++;
#endif
                        if (!commits.Contains(change.commit))
                        {
                            commits.Add(change.commit);
                        }
#if DEBUG
                        else
                        {
                            duplicateCommits++;
                        }
#endif
                    }

#if DEBUG
                    Console.WriteLine($"{commitCount} commit{(commitCount != 1 ? "s" : "")}, {duplicateCommits} duplicate{(duplicateCommits != 1 ? "s" : "")}.");
#endif
                }
#if DEBUG
                Console.WriteLine();
#endif

                for (int i = oldBuild; i <= Settings.Default.LastBuild; i++)
                {
                    buildLines.Add(i, new LinkedList<string>());

                    Console.WriteLine($"-- {i} --");
                    buildLines[i].AddLast($"-- {i} --");

                    JsonClasses.VersionBuilds temp;
                    if (i == versionInfo.builds[^1])
                    {
                        temp = builds;
                    }
                    else
                    {
                        temp = JsonSerializer.Deserialize<JsonClasses.VersionBuilds>(wc.DownloadString($"https://papermc.io/api/v2/projects/paper/versions/{projectInfo.versions[^1]}/builds/{i}"));
                    }

                    int commitCount = 0;
                    foreach (JsonClasses.Change change in temp.changes)
                    {
                        if (!commits.Contains(change.commit))
                        {
                            commits.Add(change.commit);

                            commitCount++;
                            buildLines[i].AddLast($@"https://github.com/PaperMC/Paper/commit/{change.commit}");

                            List<string> messageLines = new List<string>(change.message.Split('\n'));
                            messageLines.RemoveAll((s) => { return string.IsNullOrWhiteSpace(s); });

                            foreach (string line in messageLines)
                            {
                                buildLines[i].AddLast(line);
                            }

                            buildLines[i].AddLast("");
                        }
                    }

                    Console.WriteLine($"{commitCount} commit{(commitCount != 1 ? "s" : "")}.");
                }

                Console.WriteLine("Saving changelog.");

                for (int i = Settings.Default.LastBuild; oldBuild <= i; i--)
                {
                    foreach (string line in buildLines[i])
                    {
                        lines.AddLast(line);
                    }
                }

                File.WriteAllLines($@"{rootDirectory}\changelog.txt", lines);
            }
            else
            {
                Console.WriteLine($@"Can't find ""{cacheDirectory}\patched_{Settings.Default.LastVersion}.jar"".");
                Console.WriteLine($@"Either something happened between running Paperclip and trying to move te file,");
                Console.WriteLine($@"or Paperclip changed and Ray needs to update this program,");
                Console.WriteLine($@"or you're running an out of date version of PageSorter.");
            }

            swProgram.Stop();
            Console.WriteLine();
            Console.WriteLine($"Done, took {swProgram.Elapsed.TotalSeconds} seconds.");
            Console.Write("Press any key to exit.");
            Console.ReadKey(true);
        }

        volatile static bool finishedDownloading = false;

        static bool processingProgress => !Monitor.TryEnter(sync, 0);
        static int progreesPercentage = -1;
        static long bytesRec = -1;

        static readonly object sync = new object();
        static void UpdateDownloadProgress(DownloadProgressChangedEventArgs args)
        {
            if (args.BytesReceived > bytesRec)
            {
                progreesPercentage = args.ProgressPercentage;
                bytesRec = args.BytesReceived;
            }

            lock (sync)
            {
                string spaces = progreesPercentage < 10 ? "   " : progreesPercentage < 100 ? "  " : " ";

                int progFill = (int)Math.Floor(progreesPercentage / 5d);
                string progFillString = new string('-', progFill);

                int progEmpty = 20 - progFill;
                string progEmptyString = new string(' ', progEmpty);

                Console.SetCursorPosition(0, cursorTop - 3);
                Console.WriteLine($"{progreesPercentage}%{spaces}[{progFillString}{progEmptyString}]        ");
                Console.WriteLine($"{bytesRec / 1000d:N}KB/{fileSize / 1000d:N}KB        ");
                Console.WriteLine($"{bytesRec / 1000d / swDownload.Elapsed.TotalSeconds:N}KB/s        ");
            }
        }

        public static void CheckForError<T>(T input) where T : JsonClasses.Error
        {
            if (input.error != null)
            {
                Console.WriteLine($"Error: {input.error}");
                Console.WriteLine();

                Console.Write("Press any key to exit.");
                Console.ReadKey(true);
                Environment.Exit(0);
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
