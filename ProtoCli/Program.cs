using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace ProtoCli
{
    public class Program
    {
        private static void PrintHelp()
        {
            var helpText =
@"
This tool wraps Google's Protocol buffer compilers so that it is easier to use. 
Note that it repects folder hierarchy, so if two proto files are stored in 
/proto/A.proto' and '/proto/dir/B.proto' respectively, the output will be 
/proto/A.cs and /proto/dir/B.cs when run from '/'.
 

The current options are supported:
    - build [options] [target_directory]:  
        --output=           The root folder where compiled files will be placed

        --lang=             The output language. Options are [java,csharp,python, go]

        --namespace=        If this option is specified, the output file will 
                            be arranged in a hierarchy matching their namespace
                            from that specified in the source folder

        --file_extension=   Custom file extension to add to generated files,
                            eg. --file_extension=.g.cs will result in files 
                            ending in *.g.cs

        target_directory    Root directory to recursively look for proto files.
                            This will usually be the proto project folder        
";
            Console.WriteLine(helpText);
        }

        private static int Error(string text)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine(text);
            Console.ResetColor();
            return -1;
        }


        private static void Success(string text)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(text);
            Console.ResetColor();
        }
        private static string ToPascalCasePath(string subpath)
        {
            return subpath.Split(new[] { "_" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => char.ToUpperInvariant(s[0]) + s.Substring(1, s.Length - 1))
                .Aggregate(string.Empty, (s1, s2) => s1 + s2);
        }
        private static string ToPascalCase(string snakeCase)
        {
            return snakeCase.Split('/').Select(x => ToPascalCasePath(x)).Aggregate((x, y) => x + "/" + y);
        }

        public static int Ok => 0;

        public static int Main(string[] args)
        {
            args.Select(x => {
                Console.WriteLine(x);
                return 0;
            });

            if (args.Any(x => x.Equals("--help") || x.Equals("-h")))
            {
                PrintHelp();
                return 0;
            }

                 

            if (args.FirstOrDefault() == "build")
            {
                var argsTemp = new string[args.Length - 1];

                for (int i = 1; i < args.Length; ++i)
                {
                    argsTemp[i - 1] = args[i];
                }
                args = argsTemp;
                
                string[] keys = new string[] { "namespace", "output", "file_extension", "lang" };
                var argRegex = new Regex("^--\\w+=(\".*\"|\\S+)$");

                var argKeyRegex = new Regex(@"(?<=(^--))\w+(?=\=)+");
                var argValueRegex = new Regex("((?<=\\=)\\S+(?=$))|((?<=(\\=\")).*(?=(\"$)))");

                var argsDict = new Dictionary<string, string>();
                var pathSpecified = false;
                for (int i = 0; i < args.Length; ++i)
                {
                    var match = argRegex.IsMatch(args[i]);
                    if (i < args.Length - 1 && !match)
                    {
                        return Error($"malformed argument in position {i}. See help for details");
                    }
                    else if (match)
                    {
                        var key = argKeyRegex.Match(args[i]);
                        var val = argValueRegex.Match(args[i]);
                        if (!keys.Any(x => x.Equals(key.Value)))
                        {
                            return Error($"{key} is not a valid argument for this tool. Please see --help for details");
                        }
                        argsDict[key.Value] = val.Value;
                    }
                    else
                    {
                        pathSpecified = true;
                    }
                }

                var workingDirectoryString = "";

                if (pathSpecified)
                {
                    var possibleworkingDirectoryString = args.LastOrDefault();
                    possibleworkingDirectoryString = possibleworkingDirectoryString.Replace('\\', '/');

                    if (Directory.Exists(possibleworkingDirectoryString))
                    {
                        workingDirectoryString = possibleworkingDirectoryString;
                    }
                    else
                    {
                        return Error("directory not found");
                    }

                }
                else
                {
                    workingDirectoryString = Directory.GetCurrentDirectory();
                }

                var architectureEnum = RuntimeInformation.OSArchitecture;
                var architecture = "";

                if (architectureEnum == Architecture.Arm || architectureEnum == Architecture.Arm64)
                {
                    return Error("This tool is not supported on arm");
                }
                else if (architectureEnum == Architecture.X64)
                {
                    architecture = "x64";
                }
                else if (architectureEnum == Architecture.X86)
                {
                    architecture = "x86";
                }

                var os = "";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    os = "windows";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    os = "macosx";
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    os = "linux";
                }
                else
                {
                    return Error($"This tool does not support {RuntimeInformation.OSDescription}");
                }

                var suffix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
                var compilerPath = $"{Directory.GetParent(System.Reflection.Assembly.GetEntryAssembly().Location).FullName.Replace('\\', '/')}/protoc_{os}_{architecture}{suffix}";                
                var queue = new Queue<DirectoryInfo>();
                
                var workingDirectory = Directory.CreateDirectory(workingDirectoryString);
                Console.WriteLine(workingDirectory.FullName);
                var workingUri = new Uri(workingDirectory.FullName, UriKind.Absolute);

                queue.Enqueue(workingDirectory);
                var anyProtoFiles = false;

                var rootOutputDirectory = argsDict.ContainsKey("output") ? Directory.CreateDirectory(argsDict["output"]) : workingDirectory;

                var successful = true;
                var customOutput = argsDict.ContainsKey("output");

                while (queue.Any())
                {
                    var directory = queue.Dequeue();

                    foreach (var file in directory.EnumerateFiles().Where(x => Regex.IsMatch(x.Name, @"^.+\.proto$")))
                    {
                        anyProtoFiles = true;
                        var info = new ProcessStartInfo()
                        {
                            FileName = compilerPath
                        };
                        IList<string> argsList = new List<string>();
                        bool firstOption = true;

                        if (argsDict.ContainsKey("namespace"))
                        {
                            argsList.Add($"--csharp_opt=base_namespace=\"{argsDict["namespace"]}\"");
                            firstOption = false;
                        }

                        var fileExtension = "cs";
                        if (argsDict.ContainsKey("file_extension"))
                        {
                            fileExtension = argsDict["file_extension"];
                            if (firstOption)
                            {
                                argsList.Add($"--csharp_opt=file_extension=\"{argsDict["file_extension"]}\"");
                                firstOption = false;
                            }
                            else
                            {
                                argsList[argsList.Count - 1] += $",file_extension=\"{argsDict["file_extension"]}\"";
                            }
                        }
                        argsList.Add($"--proto_path=\"{workingDirectory.FullName}\"");



                        var fileUri = new Uri(file.FullName);

                        var relativeString = workingUri.MakeRelativeUri(new Uri(directory.FullName)).OriginalString.Replace('\\', '/');
                        var fwdIndex = relativeString.IndexOf('/');
                        fwdIndex = fwdIndex < 0 ? 0 : fwdIndex;
                        relativeString = relativeString.Substring(fwdIndex).Replace('\\', '/');

                        string outputPath;

                        if (customOutput)
                        {
                            outputPath = rootOutputDirectory.FullName.Replace('\\', '/') +"/"+ ToPascalCase(relativeString);
                        }
                        else
                        {
                            outputPath = rootOutputDirectory.FullName.Replace('\\', '/')+"/"+ relativeString;
                        }

                        var dir = Directory.CreateDirectory(outputPath);
                        argsList.Add($"--{(argsDict.ContainsKey("lang")?argsDict["lang"]:"csharp")}_out=\"{outputPath}\"");
                        argsList.Add($"\"{file.FullName.Replace('\\', '/')}\"");
                        info.Arguments = string.Join(" ", argsList);                        
                        var process = Process.Start(info);
                        process.WaitForExit();
                        if (process.ExitCode != 0)
                        {
                            successful = false;
                        }
                    }
                    foreach (var childDirectory in directory.EnumerateDirectories())
                    {
                        queue.Enqueue(childDirectory);
                    }

                }

                if (!anyProtoFiles)
                {
                    return Error("No *.proto files could be found");
                }
                else if (successful)
                {
                    Success("Sucessfully compiled *.proto files");
                    return Ok;
                }
                else
                {
                    return Error("Failed to compile all *.proto files");
                }
            }
            PrintHelp();
            return Error("Please select a valid option");
        }
    }
}