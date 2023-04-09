using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;


namespace ConsoleApp2
{
    internal class Program
    {
        static void Main(string[] args)
        {
            uint minMemory = 10000;
            uint maxMemory = 200000;
            Process process = new Process();
            process.StartInfo.FileName = "notepad.exe";
            process.Start();
            
            using (var job = new JobObject(process))
            {

                (uint, uint)? workingSetQuota = (minMemory, maxMemory);
                uint? activeProcessQuota = null;
                if (workingSetQuota.HasValue || activeProcessQuota.HasValue)
                {
                   job.SetBasicLimits(workingSetQuota, activeProcessQuota);
                }
            }
        }
    }
}
