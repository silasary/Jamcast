using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace JamCast.Controller
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static async Task Main()
        {
            ThreadPool.SetMaxThreads(32, 32);
            ThreadPool.SetMinThreads(32, 32);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ControllerForm());
        }
    }
}
