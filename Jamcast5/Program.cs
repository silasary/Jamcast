using System;
using System.Collections.Generic;
using System.Deployment.Application;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Jamcast5
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Task.Run(new Action(CheckForUpdates));
            new DumbClient().Run();
            Application.Run();
        }

        private static void CheckForUpdates()
        {
            while (true)
            {
                // Wait a minute.
                Thread.Sleep(60000);

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
                            ad.Update();
                            Application.Restart();
                        }
                        catch
                        {
                            // Can't auto-update.
                        }
                    }
                }
            }
        }

    }
}
