using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WindowsLibraryDemo.Logger.CDF
{
    public class CdfTrace : IDisposable
    {
        private static readonly Guid _Message_Guid = new Guid("1c7af7a9-d188-4604-a430-9c378087aa5c");
        private const uint ERROR_SUCCESS = 0;

        private readonly string _moduleName;
        private Guid _guid;
        private GCHandle pinnedCallbackHandle;
        private ulong handle;
        private ulong sessionHandle;
        private uint _category;
        private byte _level;

        public bool Enabled { get; private set; }

        public SeverityLevel Level
        {
            get { return (SeverityLevel)_level; }
        }

        public Category Categories
        {
            get { return (Category)_category; }
        }
        /// <summary>
        /// Trace category enumerations 
        /// </summary>
        [Flags]
        public enum Category
        {
            // Summary:
            //     ERROR class. Returns 0x1.
            Error = 1,
            //
            // Summary:
            //     INFO class. Returns 0x2.
            Info = 2,
            //
            // Summary:
            //     PRIVATE class. Returns 0x4.
            Private = 4,
            //
            // Summary:
            //     ENTRY class. Returns 0x8.
            Entry = 8,
            //
            // Summary:
            //     PERFORMANCE class. Returns 0x10.
            Performance = 16,
            //
            // Summary:
            //     WARNING class. Returns 0x40.
            Warning = 64,
            //
            // Summary:
            //     FAILURE class. Returns 0x80.
            Failure = 128,
            //
            // Summary:
            //     STARTEND class. Returns 0x100.
            StartEnd = 256
        }

        /// <summary>
        /// Trace severity level enumerations
        /// </summary>
        public enum SeverityLevel
        {
            /// <summary>
            /// Reserved. Returns 0
            /// </summary>
            Reserved = 0,
            /// <summary>
            /// Always on tracing. Critical. Returns 1
            /// </summary>
            Critical = 1,
            /// <summary>
            /// Always on tracing. AlwaysOn. Returns 2
            /// </summary>
            AlwaysOn = 2,
            /// <summary>
            /// Always on tracing. Important. Returns 3
            /// </summary>
            Important = 3,
            /// <summary>
            /// Always on tracing. Quiet. Returns 4
            /// </summary>
            Quiet = 4,
            /// <summary>
            /// Always on tracing. Debug. Returns 5
            /// </summary>
            Debug = 5,
            /// <summary>
            /// Always on tracing. Noisy. Returns 6
            /// </summary>
            Noisy = 6,
            /// <summary>
            /// Always on tracing. Verbose. Returns 7
            /// </summary>
            Verbose = 7,
            /// <summary>
            /// Always on tracing. Loud. Returns 8
            /// </summary>
            Loud = 8,
            /// <summary>
            /// Urgent. Returns 9
            /// </summary>
            Urgent = 9,
            /// <summary>
            /// Significant. Returns 10
            /// </summary>
            Significant = 10,
            /// <summary>
            /// ImportantDetailed. Returns 11
            /// </summary>
            ImportantDetailed = 11,
            /// <summary>
            /// Informational. Returns 12
            /// </summary>
            Informational = 12,
            /// <summary>
            /// InformationalDetailed. Returns 13
            /// </summary>
            InformationalDetailed = 13,
            /// <summary>
            /// Notable. Returns 14
            /// </summary>
            Notable = 14,
            /// <summary>
            /// NotableDetailed. Returns 15
            /// </summary>
            NotableDetailed = 15,
            /// <summary>
            /// Insignificant. Returns 16
            /// </summary>
            Insignificant = 16
        }

        /// <summary>
        /// Each tracable 'module' needs a name and a guid.
        /// Name is structured, eg Receiver_SelfService
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="moduleName"></param>
        public CdfTrace(Guid guid, string moduleName) : this(guid, moduleName, null)
        {
        }

        /// <summary>
        /// Create trace and provide a callbacks for changes in config.
        /// This will be called initially to indicate if tracing is already enabled.
        /// </summary>
        /// <param name="guid"></param>
        /// <param name="moduleName"></param>
        /// <param name="onChange"></param>
        public CdfTrace(Guid guid, string moduleName, TraceLevelChangeHandler onChange)
        {
            _moduleName = moduleName;
            _guid = guid;

            if (onChange != null)
                OnTracingChange += onChange;

            Advapi32.TraceGuidRegistration traceGuid = new Advapi32.TraceGuidRegistration();
            traceGuid.guid = _Message_Guid;

            Advapi32.ControlCallback controlCallback = new Advapi32.ControlCallback(ControlCallbackImpl);
            pinnedCallbackHandle = GCHandle.Alloc(controlCallback, GCHandleType.Normal);

            // CPR 252134.
            // Bug in Windows XP where the thread that calls ControlCallbackImpl has only 64k
            // of stack space.  This causes .NET's JIT compiler to run out of stack space and
            // kills the process.  To workaround this issue, call ControlCallbackImpl before
            // registering to ETW so that the call back is compiled in a thread that has enough
            // stack space.
            ignoreCallback = true;
            ControlCallbackImpl(0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

            int result = Advapi32.RegisterTraceGuids(
                controlCallback,
                IntPtr.Zero,
                ref _guid,
                1,
                ref traceGuid,
                null,
                null,
                out handle);

            if (result != 0)
            {
                System.Diagnostics.Trace.WriteLine("RegisterTraceGuids returned " + result + " for " + _guid);
                throw new System.ComponentModel.Win32Exception();
            }
        }

        public void Dispose()
        {
            if (handle != 0)
            {
                Advapi32.UnregisterTraceGuids(handle);
                handle = 0;

                if (pinnedCallbackHandle.IsAllocated)
                {
                    pinnedCallbackHandle.Free();
                }
            }
        }

        public delegate void TraceLevelChangeHandler(CdfTrace sender);

        public event TraceLevelChangeHandler OnTracingChange;

        private bool ignoreCallback;
        private uint ControlCallbackImpl(int requestCode, IntPtr context, IntPtr reserved, IntPtr buffer)
        {
            if (ignoreCallback)
            {
                ignoreCallback = false;
                return 0;
            }

            ulong loggerHandle = Advapi32.GetTraceLoggerHandle(buffer);
            if (loggerHandle == 0xFFFFFFFFFFFFFFFF)
            {
                throw new System.ComponentModel.Win32Exception();
            }

            sessionHandle = loggerHandle;

            _category = (uint)Advapi32.GetTraceEnableFlags(sessionHandle);
            _level = Advapi32.GetTraceEnableLevel(sessionHandle);

            Enabled = (requestCode == Advapi32.WMI_ENABLE_EVENTS);

            if (OnTracingChange != null)
                OnTracingChange(this);

            return 0;
        }



        /// <summary>
        /// Check if a category/level combination is enabled
        /// </summary>
        /// <param name="category"></param>
        /// <param name="level"></param>
        /// <returns></returns>
        public bool IsEnabled(Category category, SeverityLevel level)
        {
            if (Enabled && ((_category & ((byte)category)) == (byte)category) && (_level >= (int)level))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Check if a category is enabled for the default severity level (urgent)
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        public bool IsEnabled(Category category)
        {
            if (Enabled && ((_category & ((byte)category)) == (byte)category) && (_level > 0))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        //  Check tracing is enabled for the specified category/level and if so 
        //  log the message
        /// </summary>
        /// <param name="category"></param>
        /// <param name="level"></param>
        /// <param name="msg"></param>
        public void Trace(Category category, SeverityLevel level, string msg)
        {
            if (IsEnabled(category, level))
                CdfTraceMessage(1, _moduleName, (int)category, (int)level, msg);
        }

        /// <summary>
        /// Check tracing is enabled for the specified category/level and if so
        /// log a mesasge adding a submodule name
        /// </summary>
        /// <param name="submodule"></param>
        /// <param name="category"></param>
        /// <param name="level"></param>
        /// <param name="msg"></param>
        public void Trace(string submodule, Category category, SeverityLevel level, string msg)
        {
            if (IsEnabled(category, level))
                Trace_NoCheck(submodule, category, level, msg);
        }

        /// <summary>
        //  Check tracing is enabled for the specified category/level and if so 
        //  log the message
        /// </summary>
        /// <param name="category"></param>
        /// <param name="level"></param>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void Trace(Category category, SeverityLevel level, string format, params object[] args)
        {
            if (IsEnabled(category, level))
                Trace_NoCheck(null, category, level, format, args);
        }

        /// <summary>
        /// Check tracing is enabled for the specified category/level and if so
        /// log a mesasge adding a submodule name
        /// </summary>
        /// <param name="submodule"></param>
        /// <param name="category"></param>
        /// <param name="level"></param>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void Trace(string submodule, Category category, SeverityLevel level, string format, params object[] args)
        {
            if (IsEnabled(category, level))
                Trace_NoCheck(submodule, category, level, format, args);
        }


        /// <summary>
        /// Check tracing is enabled for informational messages, and if so trace
        /// </summary>
        /// <param name="msg"></param>
        public void TraceMsg(string msg)
        {
            if (IsEnabled(Category.Info, SeverityLevel.Urgent))
                CdfTraceMessage(1, _moduleName, (int)Category.Info, (int)SeverityLevel.Urgent, msg);
        }

        /// <summary>
        /// Check tracing is enabled for informational messages, and if so trace using specified format/args
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void TraceMsg(string format, params object[] args)
        {
            if (IsEnabled(Category.Info, SeverityLevel.Urgent))
            {
                Trace_NoCheck(null, Category.Info, SeverityLevel.Urgent, format, args);
            }
        }

        /// <summary>
        /// Low level trace routine that does not check tracing is enabled (caller should check)
        /// </summary>
        /// <param name="submodule">or null</param>
        /// <param name="category"></param>
        /// <param name="level"></param>
        /// <param name="format">or message</param>
        /// <param name="args">optional</param>
        public void Trace_NoCheck(string submodule, Category category, SeverityLevel level, string format, params object[] args)
        {
            string mod;

            if (submodule != null)
                mod = _moduleName + "_" + submodule;
            else
                mod = _moduleName;

            string msg;

            if (args.Length == 0)
            {
                msg = format;
            }
            else
            {
                try
                {
                    msg = String.Format(format, args);
                }
                catch (Exception)
                {
                    msg = format + " [bad args]";
                }
            }

            CdfTraceMessage(1, mod, (int)category, (int)level, msg);
        }


        public void DisableTracing()
        {
            Enabled = false;
        }
        /// <summary>
        /// Stolen from CDF.Net
        /// </summary>
        /// <param name="messageNumber"></param>
        /// <param name="moduleName"></param>
        /// <param name="flag"></param>
        /// <param name="level"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private bool CdfTraceMessage(
        ushort messageNumber,
        string moduleName,
        int flag,
        int level,
        string message)
        {
            IntPtr pTraceBuffer = IntPtr.Zero;
            IntPtr pMessageNum = IntPtr.Zero;
            IntPtr pModuleName = IntPtr.Zero;
            IntPtr pFlag = IntPtr.Zero;
            IntPtr pLevel = IntPtr.Zero;
            IntPtr pMessage = IntPtr.Zero;

            TraceBuffer traceBuff = TraceBuffer.NewTraceBuffer();
            traceBuff.Header.HistoricalContext = 0;
            traceBuff.Header.Guid = _Message_Guid;
            traceBuff.Header.Flags = 0x00120000; //WNODE_FLAG_TRACED_GUID | WNODE_FLAG_USE_MOF_PTR
            traceBuff.Header.Type = 0xFF;

            try
            {
                pMessageNum = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(ushort)));
                Marshal.WriteInt16(pMessageNum, (short)messageNumber);
                traceBuff.MofFields[0].DataPtr = (ulong)pMessageNum.ToInt64();
                traceBuff.MofFields[0].Length = (uint)Marshal.SizeOf(typeof(ushort));

                pModuleName = Marshal.StringToHGlobalUni(moduleName);
                traceBuff.MofFields[1].DataPtr = (ulong)pModuleName.ToInt64();
                traceBuff.MofFields[1].Length = (uint)(moduleName.Length + 1) * 2;

                pFlag = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(int)));
                Marshal.WriteInt32(pFlag, flag);
                traceBuff.MofFields[2].DataPtr = (ulong)pFlag.ToInt64();
                traceBuff.MofFields[2].Length = (uint)Marshal.SizeOf(typeof(int));

                pLevel = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(int)));
                Marshal.WriteInt32(pLevel, level);
                traceBuff.MofFields[3].DataPtr = (ulong)pLevel.ToInt64();
                traceBuff.MofFields[3].Length = (uint)Marshal.SizeOf(typeof(int));

                pMessage = Marshal.StringToHGlobalUni(message);
                traceBuff.MofFields[4].DataPtr = (ulong)pMessage.ToInt64();
                traceBuff.MofFields[4].Length = (uint)(message.Length + 1) * 2;

                traceBuff.Header.Size = (ushort)(Marshal.SizeOf(typeof(EventTraceHeader)) +
                    (Marshal.SizeOf(typeof(MofField)) * 5));

                // allocate unmanaged TraceBuffer memory
                pTraceBuffer = Marshal.AllocHGlobal(Marshal.SizeOf(traceBuff));
                Marshal.StructureToPtr(traceBuff, pTraceBuffer, false);

                uint result = Advapi32.TraceEvent(sessionHandle, pTraceBuffer);
                if (result != ERROR_SUCCESS)
                {
                    Debug.WriteLine("TraceMsg failed(" + result + ") " + pMessage);
                    // ignore error
                }
            }
            finally
            {
                if (pTraceBuffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pTraceBuffer);
                }
                if (pMessageNum != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pMessageNum);
                }
                if (pModuleName != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pModuleName);
                }
                if (pFlag != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pFlag);
                }
                if (pLevel != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pLevel);
                }
                if (pMessage != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pMessage);
                }
            }
            return true;
        }

    }

    /// <summary>
    /// Provides access to registry related functions in Advapi32.dll
    /// </summary>
    internal class Advapi32
    {
        public static readonly IntPtr HKEY_LOCAL_MACHINE = new IntPtr(unchecked((int)0x80000002));
        public const int KEY_QUERY_VALUE = 0x0001;
        public const int KEY_SET_VALUE = 0x0002;
        public const int KEY_CREATE_SUB_KEY = 0x0004;
        public const int KEY_ENUMERATE_SUB_KEYS = 0x0008;
        public const int KEY_NOTIFY = 0x0010;
        public const int KEY_CREATE_LINK = 0x0020;
        public const int KEY_WOW64_32KEY = 0x0200;
        public const int KEY_WOW64_64KEY = 0x0100;
        public const int KEY_WOW64_RES = 0x0300;
        public const int STANDARD_RIGHTS_READ = 0x00020000;
        public const int SYNCHRONIZE = 0x00100000;
        public const int KEY_READ = ((STANDARD_RIGHTS_READ | KEY_QUERY_VALUE | KEY_ENUMERATE_SUB_KEYS | KEY_NOTIFY) & (~SYNCHRONIZE));

        [Flags]
        public enum NotifyFilter
        {
            REG_NOTIFY_CHANGE_NAME = 1,
            REG_NOTIFY_CHANGE_ATTRIBUTES = 2,
            REG_NOTIFY_CHANGE_LAST_SET = 4,
            REG_NOTIFY_CHANGE_SECURITY = 8,
        }

        [DllImport("advapi32.dll", SetLastError = true,
            CallingConvention = CallingConvention.StdCall)]
        public static extern Int32 RegOpenKeyEx(
            IntPtr hKey,
            string subKey,
            uint options,
            int samDesired,
            out IntPtr phkResult);

        [DllImport("advapi32.dll", SetLastError = true,
            CallingConvention = CallingConvention.StdCall)]
        public static extern int RegCloseKey(
            IntPtr hKey);

        [DllImport("advapi32.dll", SetLastError = true,
            CallingConvention = CallingConvention.StdCall)]
        public static extern int RegNotifyChangeKeyValue(
            IntPtr hKey,
            bool bWatchSubtree,
            NotifyFilter dwNotifyFilter,
            IntPtr hEvent,
            bool fAsynchronous);

        public const int WMI_ENABLE_EVENTS = 4;

        public delegate uint ControlCallback(
            int requestCode,
            IntPtr context,
            IntPtr reserved,
            IntPtr buffer);

        public struct TraceGuidRegistration
        {
            public object guid;
            public IntPtr handle;
        }

        [DllImport("advapi32.dll", SetLastError = true,
            CallingConvention = CallingConvention.StdCall)]
        public static extern int RegisterTraceGuids(
            ControlCallback requestAddress,
            IntPtr requestContext,
            ref Guid controlGuid,
            int guidCount,
            ref TraceGuidRegistration traceGuidReg,
            String mofImagePath,
            String mofResourceName,
            out ulong registrationHandle);

        [DllImport("advapi32.dll", SetLastError = true,
            CallingConvention = CallingConvention.StdCall)]
        public static extern int UnregisterTraceGuids(
            ulong registrationHandle);

        [DllImport("advapi32.dll", SetLastError = true,
            CallingConvention = CallingConvention.StdCall)]
        public static extern uint TraceEvent(
            ulong sessionHandle,
            IntPtr eventTrace);

        [DllImport("advapi32.dll", SetLastError = true,
            CallingConvention = CallingConvention.StdCall)]
        public static extern ulong GetTraceLoggerHandle(IntPtr buffer);

        [DllImport("advapi32.dll", SetLastError = true,
            CallingConvention = CallingConvention.StdCall)]
        public static extern int GetTraceEnableFlags(ulong sessionHandle);

        [DllImport("advapi32.dll", SetLastError = true,
            CallingConvention = CallingConvention.StdCall)]
        public static extern byte GetTraceEnableLevel(ulong sessionHandle);

    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
    internal struct TraceBuffer
    {
        public EventTraceHeader Header;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
        public MofField[] MofFields;

        public static TraceBuffer NewTraceBuffer()
        {
            TraceBuffer buffer = new TraceBuffer();
            buffer.MofFields = new MofField[9];
            for (int i = 0; i < 9; i++)
            {
                buffer.MofFields[i] = new MofField();
            }
            return buffer;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
    internal struct EventTraceHeader
    {
        public ushort Size;
        public ushort FiledTypeFlags;
        public byte Type;
        public byte Level;
        public ushort Version;
        public ulong HistoricalContext;
        public long TimeStamp;
        public Guid Guid;
        public uint ClientContext;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
    internal struct MofField
    {
        public ulong DataPtr;
        public uint Length;
        public uint DataType;
    }
}
