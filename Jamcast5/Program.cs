using System;
using System.Deployment.Application;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Jamcast5
{
    static class Program
    {
        private static DumbClient client;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            foreach (Process proc in Process.GetProcesses())
            {
                if (proc.ProcessName.Equals(Process.GetCurrentProcess().ProcessName) && proc.Id != Process.GetCurrentProcess().Id)
                {
                    proc.Kill();
                    break;
                }
            }
            // Wait for process to close
            Thread.Sleep(2000);
            //RunMutex();
            Run();
        }

        private static void RunMutex()
        {
            Mutex mutex = new System.Threading.Mutex(false, "JamCastMutex");
            try
            {
                bool tryAgain = true;
                while (tryAgain)
                {
                    bool result = false;
                    try
                    {
                        result = mutex.WaitOne(0, false);
                    }
                    catch (AbandonedMutexException ex)
                    {
                        // No action required
                        result = true;
                    }
                    if (result)
                    {
                        // Run the application
                        tryAgain = false;
                        Run();
                    }
                    else
                    {
                        foreach (Process proc in Process.GetProcesses())
                        {
                            if (proc.ProcessName.Equals(Process.GetCurrentProcess().ProcessName) && proc.Id != Process.GetCurrentProcess().Id)
                            {
                                proc.Kill();
                                break;
                            }
                        }
                        // Wait for process to close
                        Thread.Sleep(2000);
                    }
                }
            }
            finally
            {
                if (mutex != null)
                {
                    mutex.Close();
                    mutex = null;
                }
            }
        }

        private static void Run()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Task.Run(CheckForUpdates);
            client = new DumbClient();
            client.Run();
            Application.Run(client);
        }

        private static async Task CheckForUpdates()
        {
            while (true)
            {
                // Wait a minute.
                await Task.Delay(60000);

                if (ApplicationDeployment.IsNetworkDeployed)
                {
                    Boolean updateAvailable = false;
                    ApplicationDeployment ad = ApplicationDeployment.CurrentDeployment;

                    try
                    {
                        updateAvailable = ad.CheckForUpdate();
                    }
                    catch
                    {
                        // Can't check; ignore.
                        continue;
                    }

                    if (updateAvailable)
                    {
                        try
                        {
                            client.Notify("Jamcast is updating");
                            ad.Update();
                        }
                        catch
                        {
                            client.Notify("Jamcast failed to update.");
                            // Can't auto-update.
                        }
                    }
                }
            }
        }

    }
}
