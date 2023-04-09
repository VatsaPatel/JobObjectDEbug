// --------------------------------------------------------------------------
//  <copyright file="JobObject.cs" company="Microsoft">
//      Copyright (c) Microsoft Corporation. All rights reserved.
//  </copyright>
// --------------------------------------------------------------------------

namespace ConsoleApp2
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Runtime.InteropServices;
    using Microsoft.Win32.SafeHandles;

    public class JobObject : IDisposable
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        internal static extern SafeWaitHandle CreateJobObject(IntPtr a, string lpName);

        [DllImport("kernel32.dll")]
        static extern bool SetInformationJobObject(SafeHandle hJob, JobObjectInfoType infoType, IntPtr lpJobObjectInfo, UInt32 cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool AssignProcessToJobObject(SafeHandle job, IntPtr process);

        readonly SafeWaitHandle handle;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2006:UseSafeHandleToEncapsulateNativeResources")]
        IntPtr cpuRateControlInfoPtr = IntPtr.Zero;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2006:UseSafeHandleToEncapsulateNativeResources")]
        IntPtr extendedInfoPtr = IntPtr.Zero;

        public JobObject(Process process)
        {
            this.handle = CreateJobObject(IntPtr.Zero, null);
            AssignProcessToJobObject(this.handle, process.Handle);
        }
        
        public void SetBasicLimits((uint min, uint max)? workingSet, uint? maxActiveProcesses)
        {
            var info = new JOBOBJECT_BASIC_LIMIT_INFORMATION();

            if (workingSet.HasValue)
            {
                info.LimitFlags |= (uint)JobObjectLimitInformationFlags.JOB_OBJECT_LIMIT_WORKINGSET;
                info.MinimumWorkingSetSize = new UIntPtr(workingSet.Value.min);
                info.MaximumWorkingSetSize = new UIntPtr(workingSet.Value.max);
            }

            if (maxActiveProcesses.HasValue)
            {
                info.LimitFlags |= (uint)JobObjectLimitInformationFlags.JOB_OBJECT_LIMIT_ACTIVE_PROCESS;
                info.ActiveProcessLimit = maxActiveProcesses.Value;
            }

            if (info.LimitFlags == 0) //noop
            {
                return;
            }

            var extendedInfo = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = info,
                JobMemoryLimit = (UIntPtr)10,
            };

            int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            this.extendedInfoPtr = Marshal.AllocHGlobal(length);
            Marshal.StructureToPtr(extendedInfo, this.extendedInfoPtr, false);

            if (!SetInformationJobObject(this.handle, JobObjectInfoType.ExtendedLimitInformation, this.extendedInfoPtr, (uint)length))
            {
                int errorNumber = Marshal.GetLastWin32Error();
                throw new Exception(string.Format(CultureInfo.InvariantCulture,
                    "Unable to set information.  Error: {0}", errorNumber));
            }
        }

        public void SetCpuRateLimit(uint cpuRate)
        {
            if (cpuRate <= 0 || cpuRate >= 100)
            {
                throw new ArgumentOutOfRangeException("cpuRate");
            }

            var cpuRateControlInfo = new JOBOBJECT_CPU_RATE_CONTROL_INFORMATION
            {
                ControlFlags = 0x1 | 0x4,
                CpuRate = cpuRate * 100
            };

            int length = Marshal.SizeOf(typeof(JOBOBJECT_CPU_RATE_CONTROL_INFORMATION));
            this.cpuRateControlInfoPtr = Marshal.AllocHGlobal(length);
            Marshal.StructureToPtr(cpuRateControlInfo, this.cpuRateControlInfoPtr, false);

            if (!SetInformationJobObject(this.handle, JobObjectInfoType.CpuRateControlInformation, this.cpuRateControlInfoPtr, (uint)length))
            {
                int errorNumber = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Unable to set information. Error: {0}", errorNumber));
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~JobObject()
        {
            this.Dispose(false);
        }

        void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.handle != null && !this.handle.IsInvalid)
                {
                    this.handle.Dispose();
                }
            }
            if (this.cpuRateControlInfoPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(this.cpuRateControlInfoPtr); 
            }
            if (this.extendedInfoPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(this.extendedInfoPtr);
            }
        }
    }


    #region Helper classes

    [StructLayout(LayoutKind.Sequential)]
    struct IO_COUNTERS
    {
        public UInt64 ReadOperationCount;
        public UInt64 WriteOperationCount;
        public UInt64 OtherOperationCount;
        public UInt64 ReadTransferCount;
        public UInt64 WriteTransferCount;
        public UInt64 OtherTransferCount;
    }


    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public Int64 PerProcessUserTimeLimit;
        public Int64 PerJobUserTimeLimit;
        public UInt32 LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public UInt32 ActiveProcessLimit;
        public UIntPtr Affinity;
        public UInt32 PriorityClass;
        public UInt32 SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct JOBOBJECT_CPU_RATE_CONTROL_INFORMATION
    {
        [FieldOffset(0)]
        public UInt32 ControlFlags;

        [FieldOffset(sizeof(UInt32))]

        public UInt32 CpuRate;
        [FieldOffset(sizeof(UInt32))]
        public UInt32 Weight;
    }

    public enum JobObjectInfoType
    {
        BasicLimitInformation = 2,
        BasicUIRestrictions = 4,
        SecurityLimitInformation = 5,
        EndOfJobTimeInformation = 6,
        AssociateCompletionPortInformation = 7,
        ExtendedLimitInformation = 9,
        GroupInformation = 11,
        NotificationLimitInformation = 12,
        GroupInformationEx = 14,
        CpuRateControlInformation = 15
    }

    public enum JobObjectLimitInformationFlags : uint
    {
        JOB_OBJECT_LIMIT_WORKINGSET = 1,
        JOB_OBJECT_LIMIT_ACTIVE_PROCESS = 0x00000008,
        
    }

    #endregion
}