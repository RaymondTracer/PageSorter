using PageSorter.Properties;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace PageSorter
{
    class Program
    {
        static string rootDirectory = AppDomain.CurrentDomain.BaseDirectory;
        static string JavaPath;

        static Stopwatch swProgram;
        static Stopwatch swDownload;

        static bool IsDebugBuild
        {
            get
            {
#if DEBUG
                return true;
#else
                return false;
#endif
            }
        }

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

                if (IsDebugBuild)
                {
                    temp += " [DEBUG]";
                }

                return temp;
            }
        }

        static void Main(string[] args)
        {
            // Check for important arguments first.
            foreach (string arg in args)
            {
                if (string.Equals(arg, "--Version", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Page Sorter v{Version} by Raymond Tracer");
                    Environment.Exit(0);
                }
            }

            Console.Title = $"Page Sorter v{Version} by Raymond Tracer";

            if (IsDebugBuild)
            {
                Console.WriteLine("[DEBUG MODE ENABLED]");
                Console.WriteLine();

                Console.WriteLine($"Arguments: {string.Join(" ", args)}");
                Console.WriteLine($"Current LastVerison: {Settings.Default.LastVersion}");
                Console.WriteLine($"Current LastBuild: {Settings.Default.LastBuild}");
                Console.WriteLine($"Current Debug_LastVersion: {Settings.Default.Debug_LastVersion}");
                Console.WriteLine($"Current Debug_LastBuild: {Settings.Default.Debug_LastBuild}");
                Console.WriteLine();

                Console.WriteLine("R to reset debug variables");
                Console.WriteLine("D to start a test download");
                Console.WriteLine("V to get Java version");

                Console.WriteLine();
                Console.Write("Press any key to start...");
                ConsoleKeyInfo consoleKeyInfo = Console.ReadKey(true);
                Console.Write("\n");

                if (consoleKeyInfo.Key == ConsoleKey.R)
                {
                    Settings.Default.Debug_LastVersion = "";
                    Settings.Default.Debug_LastBuild = 0;
                    Settings.Default.Save();

                    Console.WriteLine("Reset Debug_LastVersion and Debug_LastBuild.");
                }
                else if (consoleKeyInfo.Key == ConsoleKey.D)
                {
                    Console.WriteLine("Test download");
                    Download("https://speed.hetzner.de/1GB.bin", Path.GetFullPath(@".\testing\TestDownload\"));
                    PauseOnDebug();
                    Environment.Exit(0);
                }
                else if (consoleKeyInfo.Key == ConsoleKey.V)
                {
                    Console.WriteLine();

                    string[] paths_debug = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine).Split(";");

                    int i = 0;
                    foreach (string s in paths_debug)
                    {
                        if (File.Exists($@"{s}\java.exe"))
                        {
                            Console.WriteLine($@"{s}\java.exe");
                            Process javaVer = new()
                            {
                                StartInfo = new ProcessStartInfo
                                {
                                    FileName = $@"{paths_debug[i]}\java.exe",
                                    Arguments = $"-version",
                                    RedirectStandardOutput = true
                                }
                            };

                            javaVer.Start();



                            StreamReader stdout = javaVer.StandardOutput;

                            while (!stdout.EndOfStream)
                            {
                            }
                                string line = stdout.ReadLine();


                                string[] words = line.Split(" ");
                                if (words.Length > 0)
                                {
                                    if (words.Length == 3)
                                    {
                                        Console.WriteLine(words[2]);
                                    }
                                    else if (words.Length == 4)
                                    {
                                        Console.WriteLine($"{words[2]} / {words[3]}");
                                    }
                                }
                            

                            javaVer.WaitForExit();

                            Console.WriteLine();
                        }

                        i++;
                    }

                    Console.WriteLine();



                    PauseOnDebug();
                    Environment.Exit(0);
                }
            }

            string[] paths = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine).Split(";");
            foreach (string s in paths)
            {
                if (File.Exists($@"{s}\java.exe"))
                {
                    JavaPath = $@"{s}\java.exe";
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(JavaPath))
            {
                Console.WriteLine("Java not found.");
                Pause("Press any key to exit...");
                Environment.Exit(0);
            }

            swProgram = Stopwatch.StartNew();

            LinkedList<string> argsList = new(args);
            while (argsList.Count > 0)
            {
                LinkedListNode<string> first = argsList.First;

                if (string.Equals(first.Value, "--RootDirectory", StringComparison.CurrentCultureIgnoreCase))
                {
                    LinkedListNode<string> next = first.Next;

                    rootDirectory = Path.GetFullPath(next.Value);
                    Console.WriteLine($"Root directory set to: {rootDirectory}");

                    Directory.CreateDirectory(rootDirectory);

                    argsList.Remove(next);
                }
                else if (string.Equals(first.Value, "--LastVersion", StringComparison.CurrentCultureIgnoreCase))
                {
                    LinkedListNode<string> next = first.Next;

                    Settings.Default.LastVersion = next.Value;
                    Settings.Default.Save();

                    Console.WriteLine($"Last verison set to: {Settings.Default.LastVersion}");

                    argsList.Remove(next);
                }
                else if (string.Equals(first.Value, "--LastBuild", StringComparison.CurrentCultureIgnoreCase))
                {
                    LinkedListNode<string> next = first.Next;

                    Settings.Default.LastBuild = Convert.ToInt32(next.Value);
                    Settings.Default.Save();

                    Console.WriteLine($"Last build set to: {Settings.Default.LastBuild}");

                    argsList.Remove(next);
                }
                else if (string.Equals(first.Value, "--Reset", StringComparison.CurrentCultureIgnoreCase))
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

            rootDirectory += !Path.EndsInDirectorySeparator(rootDirectory) ? @"\" : "";

            // Probly best to initialize "global" local variables here.
            string workDirectory = $"{rootDirectory}{Path.DirectorySeparatorChar}work";
            string versionsDirectory = $"{workDirectory}{Path.DirectorySeparatorChar}versions";

            string LastVersion = IsDebugBuild ? "Debug_LastVersion" : "LastVersion";
            string LastBuild = IsDebugBuild ? "Debug_LastBuild" : "LastBuild";

            using WebClient wc = new();

            Console.WriteLine();
            Console.WriteLine($"Last Verison: {Settings.Default[LastVersion]}");
            Console.WriteLine($"Last Build: {Settings.Default[LastBuild]}");
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
            if (string.IsNullOrWhiteSpace((string)Settings.Default[LastVersion]))
            {
                Settings.Default[LastVersion] = projectInfo.versions[^1];
                Settings.Default[LastBuild] = Math.Max(versionInfo.builds[0], versionInfo.builds[^1] - 5);
                Settings.Default.Save();
            }
            else if (!projectInfo.versions[^1].Equals((string)Settings.Default[LastVersion]))
            {
                Settings.Default[LastVersion] = projectInfo.versions[^1];
                Settings.Default[LastBuild] = 0;
                Settings.Default.Save();

                oldVerInfo = $", Updated from {Settings.Default[LastVersion]}";
            }

            int oldBuild = 0;
            if ((int)Settings.Default[LastBuild] == versionInfo.builds[^1])
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
                    Console.Write("Press any key to exit...");
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
            else if ((int)Settings.Default[LastBuild] > versionInfo.builds[^1])
            {
                oldBuild = Math.Max(versionInfo.builds[0], versionInfo.builds[^1] - 5);
                Settings.Default[LastBuild] = versionInfo.builds[^1];
                Settings.Default.Save();
            }
            else
            {
                oldBuild = (int)Settings.Default[LastBuild];
                Settings.Default[LastBuild] = versionInfo.builds[^1];
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
                    FileName = JavaPath,
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

            string filename = $"paper-{Settings.Default[LastVersion]}.jar";
            if (File.Exists($@"{versionsDirectory}\{Settings.Default[LastVersion]}\{filename}"))
            {
                File.Move($@"{versionsDirectory}\{Settings.Default[LastVersion]}\{filename}", $@"{rootDirectory}\..\paperclip.jar", true);

                if ((int)Settings.Default[LastBuild] - oldBuild == 0)
                {
                    oldBuild -= 5;
                }

                oldBuild = Math.Max(versionInfo.builds[0], oldBuild);

                Console.WriteLine($"Getting changelog for builds {oldBuild} to {Settings.Default[LastBuild]}");

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

                for (int i = oldBuild; i <= (int)Settings.Default[LastBuild]; i++)
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

                for (int i = (int)Settings.Default[LastBuild]; oldBuild <= i; i--)
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
                Console.WriteLine($@"Can't find ""{versionsDirectory}\{Settings.Default[LastVersion]}\{filename}"".");
                Console.WriteLine($@"Either something happened between running Paperclip and trying to move te file,");
                Console.WriteLine($@"or Paperclip changed and Ray needs to update this program,");
                Console.WriteLine($@"or you're running an out of date version of Page Sorter.");
            }

            swProgram.Stop();
            Console.WriteLine();
            Console.WriteLine($"Done, took {swProgram.Elapsed.TotalSeconds} seconds.");
            Console.Write("Press any key to exit...");
            Console.ReadKey(true);
        }

        private static void Download(string url, string filePath)
        {
            string fileName = Path.GetFileName(filePath);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = url.Split("/")[^1];

                Directory.CreateDirectory(filePath);
                filePath += $"{(Path.EndsInDirectorySeparator(filePath) ? "" : Path.DirectorySeparatorChar)}{fileName}";
            }
            else
            {
                Directory.CreateDirectory(string.Join(Path.DirectorySeparatorChar, filePath.Split(Path.DirectorySeparatorChar).SkipLast(1)));
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

                Pause("Press any key to exit...");
                Environment.Exit(1);
            }

            long fileSize = Convert.ToInt64(wcDebug.ResponseHeaders["Content-Length"]);

            Console.WriteLine($"Progress:       0%   [                    ]");
            Console.WriteLine($"Remaining:      0 KiB/{fileSize / 1024d} KiB");
            Console.WriteLine($"Download Speed: 0 KiB/s");
            Console.WriteLine($"ETA:            00:00:00");
            Console.WriteLine($"Time Elapsed:   00:00:00");

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
                    Pause("Press any key to exit...");
                    Environment.Exit(1);
                }
            });

            bool downloadFinished = false;

            int lastProgressLength = 0;
            int lastRemainingLength = 0;
            int lastDownloadSpeedLength = 0;
            int lastETALength = 0;
            int lastTimeElapsedLength = 0;

            while (!downloadFinished)
            {
                downloadFinished = bytesRecieved >= fileSize;

                double progreesPercentage = (double)bytesRecieved / fileSize * 100d;
                string spaces = progreesPercentage < 10 ? "   " : progreesPercentage < 100 ? "  " : " ";

                int progFill = (int)Math.Floor(progreesPercentage / 5d);
                string progFillString = new('-', progFill);

                int progEmpty = 20 - progFill;
                string progEmptyString = new(' ', progEmpty);

                Console.SetCursorPosition(0, cursorTop - 5);

                string progress = $"Progress:       {progreesPercentage:0.00}%{spaces}[{progFillString}{progEmptyString}]";
                int line1Length = progress.Length;
                if (lastProgressLength > progress.Length) progress += new string(' ', Math.Max(0, lastProgressLength - progress.Length));
                lastProgressLength = line1Length;
                Console.WriteLine(progress);

                string remaining = $"Remaining:      {bytesRecieved / 1024d:N} KiB/{fileSize / 1024d:N} KiB";
                int remainingLength = remaining.Length;
                if (lastRemainingLength > remaining.Length) remaining += new string(' ', Math.Max(0, lastRemainingLength - remaining.Length));
                lastRemainingLength = remainingLength;
                Console.WriteLine(remaining);

                string dlSpeed = $"Download Speed: {bytesRecieved / 1024d / swDownload.Elapsed.TotalSeconds:N} KiB/s";
                int dlSpeedLength = dlSpeed.Length;
                if (lastDownloadSpeedLength > dlSpeed.Length) dlSpeed += new string(' ', Math.Max(0, lastDownloadSpeedLength - dlSpeed.Length));
                lastDownloadSpeedLength = dlSpeedLength;
                Console.WriteLine(dlSpeed);

                TimeSpan eta = TimeSpan.FromSeconds(Math.Clamp((fileSize - bytesRecieved) / (bytesRecieved / swDownload.Elapsed.TotalSeconds), TimeSpan.MinValue.TotalSeconds, TimeSpan.MaxValue.TotalSeconds));
                string etaLine = $"ETA:            {eta}";
                int etaLength = etaLine.Length;
                if (lastETALength > etaLine.Length) etaLine += new string(' ', Math.Max(0, lastETALength - etaLine.Length));
                lastETALength = etaLength;
                Console.WriteLine(etaLine);

                string timeElapsed = $"Time Elapsed:   {swDownload.Elapsed}";
                int teLength = timeElapsed.Length;
                if (lastETALength > timeElapsed.Length) timeElapsed += new string(' ', Math.Max(0, lastTimeElapsedLength - timeElapsed.Length));
                lastTimeElapsedLength = teLength;
                Console.Write(timeElapsed);
            }

            swDownload.Stop();
            Console.Write("\n");
            Console.WriteLine();
            Console.WriteLine($"Download Competed!");

            Console.WriteLine();
            Console.WriteLine($"Saving file.");

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            if (!ByteArrayToFile(filePath, data))
            {
                Pause("Press any key to exit...");
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

                Console.Write("Press any key to exit...");
                Console.ReadKey(true);
                Environment.Exit(0);
            }
        }

        public static void Pause(string pauseMessage)
        {
            Console.Write(pauseMessage);
            Console.ReadKey(true);
            Console.Write("\n");
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
            catch (Exception e)
            {
                if (e is FileNotFoundException)
                {
                    return false;
                }
            }

            return true;
        }

        [Conditional("DEBUG")]
        public static void PauseOnDebug()
        {
            Console.Write("Press any key to continue...");
            Console.ReadKey(true);
            Console.Write("\n");
        }
    }
}
