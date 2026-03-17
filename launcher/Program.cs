using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Launcher
{
    class Program
    {
        private const string REPO_RAW_URL = "https://raw.githubusercontent.com/alfredodelperu/whatsapp-apex-sync/main";
        private const string VERSION_FILE_URL = $"{REPO_RAW_URL}/version.txt";
        private const string EXE_DOWNLOAD_URL = $"{REPO_RAW_URL}/backend/bin/Release/net8.0/win-x64/publish/WhatsAppTranscriptor.exe";
        
        private const string LOCAL_EXE_NAME = "WhatsAppTranscriptor.exe";
        private const string LOCAL_VERSION_FILE = "version.txt";

        static async Task Main(string[] args)
        {
            Console.WriteLine("=========================================================");
            Console.WriteLine("        WHATSAPP TRANSCRIPTOR - AUTO-UPDATER V1.0        ");
            Console.WriteLine("=========================================================");
            
            try
            {
                using var client = new HttpClient();
                
                // 1. Fetch remote version
                Console.WriteLine("Checking for updates on GitHub...");
                
                // Add a slight timeout so it doesn't hang forever if no internet
                client.Timeout = TimeSpan.FromSeconds(10);
                
                string remoteVersionStr = "0.0";
                try 
                {
                    remoteVersionStr = await client.GetStringAsync(VERSION_FILE_URL);
                    remoteVersionStr = remoteVersionStr.Trim();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Warning] Could not connect to GitHub. Proceeding with offline version. Error: {ex.Message}");
                }

                // 2. Read local version
                string localVersionStr = "0.0";
                if (File.Exists(LOCAL_VERSION_FILE))
                {
                    localVersionStr = File.ReadAllText(LOCAL_VERSION_FILE).Trim();
                }

                if (Version.TryParse(remoteVersionStr, out Version remoteVersion) && 
                    Version.TryParse(localVersionStr, out Version localVersion))
                {
                    Console.WriteLine($"Local Version: {localVersion} | Remote Version: {remoteVersion}");
                    
                    if (remoteVersion > localVersion)
                    {
                        Console.WriteLine("New version detected! Downloading update...");
                        
                        // Kill any running instances of the app before overwriting
                        KillRunningProcesses(LOCAL_EXE_NAME.Replace(".exe", ""));

                        // Download the new executable
                        byte[] exeBytes = await client.GetByteArrayAsync(EXE_DOWNLOAD_URL);
                        
                        // Write to temp file first to prevent corruption
                        string tempExe = LOCAL_EXE_NAME + ".tmp";
                        await File.WriteAllBytesAsync(tempExe, exeBytes);
                        
                        // Replace the old executable safely
                        if (File.Exists(LOCAL_EXE_NAME)) File.Delete(LOCAL_EXE_NAME);
                        File.Move(tempExe, LOCAL_EXE_NAME);
                        
                        // Update local version file
                        await File.WriteAllTextAsync(LOCAL_VERSION_FILE, remoteVersion.ToString());
                        Console.WriteLine($"Update to v{remoteVersion} successful!");
                    }
                    else
                    {
                        Console.WriteLine("You are running the latest version.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error during update process]: {ex.Message}");
            }

            // 3. Launch the main application
            Console.WriteLine($"Launching {LOCAL_EXE_NAME}...");
            if (File.Exists(LOCAL_EXE_NAME))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = LOCAL_EXE_NAME,
                    UseShellExecute = true,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                });
            }
            else
            {
                Console.WriteLine($"[FATAL] Cannot find {LOCAL_EXE_NAME} in the current directory.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            
            // Auto exit
        }

        static void KillRunningProcesses(string processName)
        {
            var processes = Process.GetProcessesByName(processName);
            foreach (var process in processes)
            {
                Console.WriteLine($"Killing existing process: {process.Id}");
                try
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
                catch { /* Ignore errors if process already exited or access denied */ }
            }
        }
    }
}
