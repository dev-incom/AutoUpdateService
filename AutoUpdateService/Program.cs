using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace AutoUpdateService
{
    internal static class Program
    {
        /// <summary>
        /// 해당 애플리케이션의 주 진입점입니다.
        /// </summary>
        static void Main(String[] args)
        {
            if (Environment.UserInteractive)
            {
                Console.WriteLine("콘솔");
                AutoUpdateService autoUpdateService = new AutoUpdateService();
                autoUpdateService.TestStartupAndStop(args);
            }
            else
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                    new AutoUpdateService()
                };
                ServiceBase.Run(ServicesToRun);
            }
        }
    }
}
