using System;
using System.Diagnostics;
using Windows.Storage;

namespace FullTrustLauncher
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var executable = (string)ApplicationData.Current.LocalSettings.Values["LaunchPath"];
                var arguments = (string)ApplicationData.Current.LocalSettings.Values["LaunchArguments"];

                //var executable = $"C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe";
                //var arguments = "-command \"cd C:\\AppData\"";

                //Process process = new Process();
                //ProcessStartInfo startInfo = new ProcessStartInfo();
                //startInfo.FileName = executable;
                //startInfo.Arguments = arguments;
                //process.StartInfo = startInfo;
                //process.Start();

                Process.Start(executable, arguments);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.ReadLine();
            }
        }
    }
}
