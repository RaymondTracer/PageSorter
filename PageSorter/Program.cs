﻿using PageSorter.Properties;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace PageSorter
{
    class Program
    {
        static string rootDirectory = AppDomain.CurrentDomain.BaseDirectory;

        static Stopwatch swProgram;
        static Stopwatch swDownload;

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
            // Check for important arguments first.
            foreach (string arg in args)
            {
                if (string.Equals(arg, "-Version", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Page Sorter v{Version} by Raymond Tracer");
                    Environment.Exit(0);
                }
            }

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
            else if (consoleKeyInfo.Key == ConsoleKey.D)
            {
                Console.WriteLine("Test download");
                Download("https://speed.hetzner.de/1GB.bin", Path.GetTempPath());
                PauseOnDebug();
                Environment.Exit(0);
            }
#endif

            swProgram = Stopwatch.StartNew();

            LinkedList<string> argsList = new(args);
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
                else if (string.Equals(first.Value, "-Reset", StringComparison.OrdinalIgnoreCase))
                {
                    Settings.Default.LastVersion = "";
                    Settings.Default.LastBuild = 0;
                    Settings.Default.Save();

                    Console.WriteLine("Reset LastVersion and LastBuild.");
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

            using WebClient wc = new();

            Console.WriteLine();
            Console.WriteLine($"Last Verison: {Settings.Default.LastVersion}");
            Console.WriteLine($"Last Build: {Settings.Default.LastBuild}");
            Console.WriteLine();

            Console.WriteLine("Getting latest version of Minecraft currently supported by PaperMC.");
            JsonClasses.ProjectInfo projectInfo = JsonSerializer.Deserialize<JsonClasses.ProjectInfo>(wc.DownloadString("https://papermc.io/api/v2/projects/paper"));
            ExitOnError(projectInfo);
            Console.WriteLine($"Found Verison: {projectInfo.versions[^1]}");

            Console.WriteLine("Getting latest build number.");
            JsonClasses.VersionInfo versionInfo = JsonSerializer.Deserialize<JsonClasses.VersionInfo>(wc.DownloadString($"https://papermc.io/api/v2/projects/paper/versions/{projectInfo.versions[^1]}"));
            ExitOnError(versionInfo);
            Console.WriteLine($"Found Build: {versionInfo.builds[^1]}");

            Console.WriteLine("Getting latest download filename.");
            JsonClasses.VersionBuilds builds = JsonSerializer.Deserialize<JsonClasses.VersionBuilds>(wc.DownloadString($"https://papermc.io/api/v2/projects/paper/versions/{projectInfo.versions[^1]}/builds/{versionInfo.builds[^1]}"));
            ExitOnError(builds);
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

            Download(downloadURL, downloadFilePath);

            Directory.CreateDirectory(workDirectory);
            File.Move(downloadFilePath, $"{workDirectory}{Path.DirectorySeparatorChar}{builds.downloads.application.name}");

            Console.WriteLine("Running Paperclip.");
            Console.WriteLine();

            Process java = new()
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

                if (Settings.Default.LastBuild - oldBuild == 0)
                {
                    oldBuild -= 5;
                }

                oldBuild = Math.Max(versionInfo.builds[0], oldBuild);

                Console.WriteLine($"Getting changelog for builds {oldBuild} to {Settings.Default.LastBuild}");

                List<string> commits = new();
                Dictionary<int, LinkedList<string>> buildLines = new();
                LinkedList<string> lines = new();

                if (oldBuild > versionInfo.builds[0])
                {

                    for (int i = oldBuild - 1; i > versionInfo.builds[0]; i--)
                    {
#if DEBUG
                        Console.WriteLine($"[- {i} -]");

                        int commitCount = 0;
#endif

                        if (versionInfo.builds.Contains(i))
                        {
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
                            }

#if DEBUG
                            Console.WriteLine($"{commitCount} commit{(commitCount != 1 ? "s" : "")}.");

#endif

                            break;
                        }
#if DEBUG
                        else
                        {
                            Console.WriteLine("(Doesn't exist!)");
                        }
#endif
                    }
                }
#if DEBUG
                Console.WriteLine();
#endif

                for (int i = oldBuild; i <= Settings.Default.LastBuild; i++)
                {
                    buildLines.Add(i, new LinkedList<string>());

                    Console.WriteLine($"-- {i} --");
                    buildLines[i].AddLast($"-- {i} --");

                    if (versionInfo.builds.Contains(i))
                    {
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

                                List<string> messageLines = new(change.message.Split('\n'));
                                for (int j = messageLines.Count - 1; j >= 0; j--)
                                {
                                    if (string.IsNullOrWhiteSpace(messageLines[j]))
                                    {
                                        messageLines.RemoveAt(j);
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }

                                foreach (string line in messageLines)
                                {
                                    buildLines[i].AddLast(line);
                                }

                                buildLines[i].AddLast("");
                            }
                        }

                        Console.WriteLine($"{commitCount} commit{(commitCount != 1 ? "s" : "")}.");
                    }
                    else
                    {
                        buildLines[i].AddLast("(Doesn't exist!)");
                        Console.WriteLine("(Doesn't exist!)");

                        buildLines[i].AddLast("");

                    }
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
                Console.WriteLine($@"or you're running an out of date version of Page Sorter.");
            }

            swProgram.Stop();
            Console.WriteLine();
            Console.WriteLine($"Done, took {swProgram.Elapsed.TotalSeconds} seconds.");
            Console.Write("Press any key to exit.");
            Console.ReadKey(true);
        }

        private static bool IsDirectory(string path)
        {
            return File.GetAttributes(path).HasFlag(FileAttributes.Directory);
        }

        private static void Download(string url, string filePath)
        {
            if (IsDirectory(filePath))
            {
                filePath += $"{Path.DirectorySeparatorChar}{url.Split("/")[^1]}";
            }

#if DEBUG
            Console.WriteLine($"URL: {url}");
            Console.WriteLine($"Download path: {filePath}");
#endif

            using WebClient wcDebug = new();

            Stream stream = null;
            try
            {
                stream = wcDebug.OpenRead(url);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to URL: {url}");
                Console.WriteLine(ex);
                Console.Write("Press any key to exit...");
                Console.ReadKey(true);
                Console.Write("\n");
                Environment.Exit(1);
            }

            long fileSize = Convert.ToInt64(wcDebug.ResponseHeaders["Content-Length"]);

            Console.WriteLine($"0%   [                    ]");
            Console.WriteLine($"0KB/{fileSize / 1000d}KB");
            Console.WriteLine($"0KB/s");
            Console.WriteLine($"ETA: ");
            int cursorTop = Console.CursorTop;

            swDownload = Stopwatch.StartNew();
            long bytesRecieved = 0;

            byte[] data = new byte[fileSize];
            int currentByte = 0;
            Task.Run(() =>
            {
                while (stream.CanRead && (currentByte = stream.ReadByte()) != -1)
                {
                    data[bytesRecieved++] = (byte)currentByte;
                }

                if (!stream.CanRead)
                {
                    Console.WriteLine("Connection dropped.");
                    Console.Write("Press any key to exit...");
                    Console.ReadKey(true);
                    Console.Write("\n");
                    Environment.Exit(1);
                }
            });

            int lastLine1Length = 0;
            int lastLine2Length = 0;
            int lastLine3Length = 0;
            int lastLine4Length = 0;

            while (currentByte != -1)
            {
                double progreesPercentage = (double)bytesRecieved / fileSize * 100d;
                string spaces = progreesPercentage < 10 ? "   " : progreesPercentage < 100 ? "  " : " ";

                int progFill = (int)Math.Floor(progreesPercentage / 5d);
                string progFillString = new('-', progFill);

                int progEmpty = 20 - progFill;
                string progEmptyString = new(' ', progEmpty);

                Console.SetCursorPosition(0, cursorTop - 4);

                string line1 = $"{progreesPercentage:0.00}%{spaces}[{progFillString}{progEmptyString}]";
                int line1Length = line1.Length;
                if (lastLine1Length > line1.Length) line1 += new string(' ', lastLine1Length - line1.Length);
                lastLine1Length = line1Length;
                Console.WriteLine(line1);

                string line2 = $"{bytesRecieved / 1000d:N}KB/{fileSize / 1000d:N}KB";
                int line2Length = line2.Length;
                if (lastLine2Length > line2.Length) line2 += new string(' ', lastLine2Length - line2.Length);
                lastLine2Length = line2Length;
                Console.WriteLine(line2);

                string line3 = $"{bytesRecieved / 1000d / swDownload.Elapsed.TotalSeconds:N}KB/s";
                int line3Length = line3.Length;
                if (lastLine3Length > line3.Length) line3 += new string(' ', lastLine3Length - line3.Length);
                lastLine3Length = line3Length;
                Console.WriteLine(line3);

                TimeSpan eta = TimeSpan.FromSeconds(Math.Clamp((fileSize - bytesRecieved) / (bytesRecieved / swDownload.Elapsed.TotalSeconds), TimeSpan.MinValue.TotalSeconds, TimeSpan.MaxValue.TotalSeconds));
                string line4 = $"ETA: {eta}";
                int line4Length = line4.Length;
                if (lastLine4Length > line4.Length) line4 += new string(' ', lastLine4Length - line4.Length);
                lastLine4Length = line4Length;
                Console.Write(line4);
            }

            swDownload.Stop();
            Console.Write("\n");
            Console.WriteLine();
            Console.WriteLine($"Download Competed! Took {swDownload.Elapsed.TotalSeconds} seconds.");
            
            Console.WriteLine();
            Console.WriteLine($"Saving file.");

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            if (!ByteArrayToFile(filePath, data))
            {
                Console.Write("Press any key to exit...");
                Console.ReadKey(true);
                Console.Write("\n");
                Environment.Exit(1);
            }

            data = null;
        }

        public static bool ByteArrayToFile(string filePath, byte[] byteArray)
        {
            try
            {
                using FileStream fs = new(filePath, FileMode.CreateNew, FileAccess.Write);
                fs.Write(byteArray, 0, byteArray.Length);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving file: {filePath}");
                Console.WriteLine(ex);
                return false;
            }
        }

        public static void ExitOnError<T>(T input) where T : JsonClasses.Error
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

        public static void PauseOnDebug()
        {
#if DEBUG
            Console.Write("Press any key to continue...");
            Console.ReadKey(true);
            Console.Write("\n");
#endif
        }
    }
}
