using WindowsLibraryDemo.Logger.CDF;
using WindowsLibraryDemo.Logger.Tracing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;


namespace WindowsLibraryDemo

{

    /// <summary>
    /// Each instance of Tracer has 'Kinds' enabled. Each trace statement has an implicit or explicit
    /// kind and is only logged if the kind is enabled. More trace kinds can be added as necessary
    /// </summary>
    [Flags]
    public enum TraceKind
    {
        None = 0,
        EntryExit = 1,
        Message = 2,
        Detail = 4,
        Error = 8,
        Debug = 16,
        NetworkError = 32,
        SysCall = 64,
        BlockEntry = 128,
        BlockExit = 256,
        Timing = 512,
        Sensitive = 1024,
        Call = 2048,
        Continuos = 4096,
        AOL = Error | Detail | Message | EntryExit | NetworkError | SysCall | BlockExit | BlockEntry | Timing | Call | Continuos,
        All = Error | Detail | Message | EntryExit | Debug | NetworkError | SysCall | BlockExit | BlockEntry | Timing | Call | Continuos  //NB not Sensitive
    }

    //This is the class which contains all the required registry utility functions
    public static class TraceRegistry
    {
        public static bool ReportAllLaunches = false;
        public static bool EnableProblemReporter = true;
    }
    /// <summary>
    /// New Tracing module designed to eventually become a wrapper for CDF
    /// Each instance of the tracer class represents a module that can be traced
    /// </summary>
    public class TraceModule
    {
        #region Classes

        internal class IndentBlock : IDisposable
        {
            private object info;
            private TraceModule parent;

            internal IndentBlock(TraceModule parent, object info)
            {
                TraceModule.Indent();
                this.parent = parent;
                this.info = info;
            }

            public void Dispose()
            {
                TraceModule.Outdent();
                if (info != null)
                {
                    if (info is string)
                    {
                        parent.RecordTrace(TraceKind.BlockExit, parent.moduleNamePrettyPrint, (string)info);
                    }
                    else if (info is DateTime)
                    {
                        TimeSpan period = DateTime.Now - ((DateTime)info);
                        parent.RecordTrace(TraceKind.BlockExit | TraceKind.Timing, parent.moduleNamePrettyPrint,
                                                "}" + period);
                    }
                }
            }
        }

        internal class NullDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }

        #endregion

        #region Instance

        public TraceKind Mode = TraceKind.Error;

        // our name formated ready for printing as 8 chars long
        private readonly string moduleNamePrettyPrint;


        public TraceModule(string name)
        {
            this.moduleNamePrettyPrint = (name + "        ").Substring(0, 8);
        }

        /// <summary>
        /// Test is tracing is enabled for a particular 
        /// </summary>
        /// <param name="opt"></param>
        /// <returns></returns>
        public bool IsEnabled(TraceKind opt)
        {
            return (((Mode | TraceKind.AOL) & opt) == opt);
        }

        /// <summary>
        /// Generic trace statement
        /// </summary>
        /// <param name="kind">Kind of trace statement eg Kind.Error</param>
        /// <param name="format">Format string, or simple string message</param>
        /// <param name="args">Additional arguments to format message</param>
        public void Trace(TraceKind kind, string format, params object[] args)
        {
            if (IsEnabled(kind))
            {
                RecordTrace(kind, _Format(format, args));
            }
        }

        /// <summary>
        /// Basic trace message. Equivalent to Trace(Kind.Message,...)
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void Trace(string format, params object[] args)
        {
            Trace(TraceKind.Message, format, args);
        }

        /// <summary>
        /// Cdf terminology for Trace()
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void CdfTraceMsg(string format, params object[] args)
        {
            Trace(TraceKind.Message, format, args);
        }

        /// <summary>
        /// Writes trace message to underlying trace mechanism.
        /// CLASS = Category.Error, LEVEL = SeverityLevel.Critical
        /// </summary>
        /// <param name="format">Trace message format</param>
        /// <param name="args">Array of arguments.</param>
        public void AOLogCriticalError(string format, params object[] args)
        {
            AOLRecordTrace(TraceKind.Continuos, CdfTrace.Category.Error, CdfTrace.SeverityLevel.Critical, _Format(format, args));
        }

        /// <summary>
        /// Writes trace message to underlying trace mechanism.
        /// CLASS = Category.Warning, LEVEL = SeverityLevel.Critical
        /// </summary>
        /// <param name="format">Trace message format</param>
        /// <param name="args">Array of arguments.</param>
        public void AOLogCriticalWarning(string format, params object[] args)
        {
            AOLRecordTrace(TraceKind.Continuos, CdfTrace.Category.Warning, CdfTrace.SeverityLevel.Critical, _Format(format, args));
        }

        /// <summary>
        /// Writes trace message to underlying trace mechanism.
        /// CLASS = Category.Failure, LEVEL = SeverityLevel.Critical
        /// </summary>
        /// <param name="format">Trace message format</param>
        /// <param name="args">Array of arguments.</param>
        public void AOLogCriticalFailure(string format, params object[] args)
        {
            AOLRecordTrace(TraceKind.Continuos, CdfTrace.Category.Failure, CdfTrace.SeverityLevel.Critical, _Format(format, args));
        }

        /// <summary>
        /// Writes trace message to underlying trace mechanism.
        /// CLASS = Category.Info, LEVEL = SeverityLevel.Critical
        /// </summary>
        /// <param name="format">Trace message format</param>
        /// <param name="args">Array of arguments.</param>
        public void AOLogCriticalInfo(string format, params object[] args)
        {
            AOLRecordTrace(TraceKind.Continuos, CdfTrace.Category.Info, CdfTrace.SeverityLevel.Critical, _Format(format, args));
        }

        /// <summary>
        /// Writes trace message to underlying trace mechanism.
        /// CLASS = Category.StartEnd, LEVEL = SeverityLevel.Critical
        /// </summary>
        /// <param name="format">Trace message format</param>
        /// <param name="args">Array of arguments.</param>
        public void AOLogCriticalStartEnd(string format, params object[] args)
        {
            AOLRecordTrace(TraceKind.Continuos, CdfTrace.Category.StartEnd, CdfTrace.SeverityLevel.Critical, _Format(format, args));
        }

        /// <summary>
        /// Writes trace message to underlying trace mechanism.
        /// CLASS = Category.Error, LEVEL = SeverityLevel.Important
        /// </summary>
        /// <param name="format">Trace message format</param>
        /// <param name="args">Array of arguments.</param>
        public void AOLogImportantError(string format, params object[] args)
        {
            AOLRecordTrace(TraceKind.Continuos, CdfTrace.Category.Error, CdfTrace.SeverityLevel.Important, _Format(format, args));
        }

        /// <summary>
        /// Writes trace message to underlying trace mechanism.
        /// CLASS = Category.Warning, LEVEL = SeverityLevel.Important
        /// </summary>
        /// <param name="format">Trace message format</param>
        /// <param name="args">Array of arguments.</param>
        public void AOLogImportantWarning(string format, params object[] args)
        {
            AOLRecordTrace(TraceKind.Continuos, CdfTrace.Category.Warning, CdfTrace.SeverityLevel.Important, _Format(format, args));
        }

        /// <summary>
        /// Writes trace message to underlying trace mechanism.
        /// CLASS = Category.Failure, LEVEL = SeverityLevel.Important
        /// </summary>
        /// <param name="format">Trace message format</param>
        /// <param name="args">Array of arguments.</param>
        public void AOLogImportantFailure(string format, params object[] args)
        {
            AOLRecordTrace(TraceKind.Continuos, CdfTrace.Category.Failure, CdfTrace.SeverityLevel.Important, _Format(format, args));
        }

        /// <summary>
        /// Writes trace message to underlying trace mechanism.
        /// CLASS = Category.Info, LEVEL = SeverityLevel.Important
        /// </summary>
        /// <param name="format">Trace message format</param>
        /// <param name="args">Array of arguments.</param>
        public void AOLogImportantInfo(string format, params object[] args)
        {
            AOLRecordTrace(TraceKind.Continuos, CdfTrace.Category.Info, CdfTrace.SeverityLevel.Important, _Format(format, args));
        }

        /// <summary>
        /// Writes trace message to underlying trace mechanism.
        /// CLASS = Category.StartEnd, LEVEL = SeverityLevel.Important
        /// </summary>
        /// <param name="format">Trace message format</param>
        /// <param name="args">Array of arguments.</param>
        public void AOLogImportantStartEnd(string format, params object[] args)
        {
            AOLRecordTrace(TraceKind.Continuos, CdfTrace.Category.StartEnd, CdfTrace.SeverityLevel.Important, _Format(format, args));
        }

        /// <summary>
        /// Trace an error. Equivalent to Trace(Kind.Error,....)
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void Error(string format, params object[] args)
        {
            Trace(TraceKind.Error, format, args);
        }

        /// <summary>
        /// Trace an call that completes with an error. 
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void CallCompletedWithError(string format, params object[] args)
        {
            Trace(TraceKind.Error | TraceKind.Call, format, args);
            ProblemReporter.CallComplete();
        }

        /// <summary>
        /// Trace an error passing an exception to give more detail
        /// </summary>
        /// <param name="e">The exception to dump</param>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void Error(Exception e, string format, params object[] args)
        {
            if (IsEnabled(TraceKind.Error))
            {
                RecordTrace(TraceKind.Error, _Format(format, args));
                if (e != null)
                {
                    RecordTrace(TraceKind.Error, e.Message);
                    RecordTrace(TraceKind.Error, e.StackTrace);

                    if (e.InnerException != null)
                    {
                        Error(e.InnerException, "InnerException");
                    }
                }
            }
        }


        /// <summary>
        /// Trace an error passing an exception to give more detail and report exception to Sentry
        /// Executables should make sure Sentry binaries are resolved and it is initialised before calling this method
        /// </summary>
        /// <param name="e">The exception to dump</param>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void FatalError(Exception e, string format, params object[] args)
        {
            if (IsEnabled(TraceKind.Error))
            {
                RecordTrace(TraceKind.Error, _Format(format, args));
                if (e != null)
                {
                    RecordTrace(TraceKind.Error, e.Message);
                    RecordTrace(TraceKind.Error, e.StackTrace);

                    if (e.InnerException != null)
                    {
                        Error(e.InnerException, "InnerException");
                    }
                }
            }
        }



        /// <summary>
        /// Trace an error passing an exception to give more detail and report exception to Sentry
        /// Executables should make sure Sentry binaries are resolved and it is initialised before calling this method
        /// </summary>
        /// <param name="e">The exception to dump</param>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void CallCompletedWithError(Exception e, string format, params object[] args)
        {
            if (IsEnabled(TraceKind.Error))
            {
                RecordTrace(TraceKind.Error | TraceKind.Call, _Format(format, args));
                if (e != null)
                {
                    RecordTrace(TraceKind.Error | TraceKind.Call, e.Message);

                    if (e.InnerException != null)
                    {
                        RecordTrace(TraceKind.Error | TraceKind.Call, e.StackTrace);
                        CallCompletedWithError(e.InnerException, "InnerException");
                    }
                    else
                    {
                        RecordTrace(TraceKind.Error | TraceKind.Call, e.StackTrace);
                        ProblemReporter.CallComplete();
                    }
                }
            }
        }



        /// <summary>
        /// Trace an error related to a network issue.
        /// </summary>
        /// <param name="e">An exception to dump</param>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void NetworkError(Exception e, string format, params object[] args)
        {
            if (IsEnabled(TraceKind.NetworkError))
            {
                RecordTrace(TraceKind.NetworkError, _Format(format, args));
                if (e != null)
                {
                    RecordTrace(TraceKind.NetworkError, e.Message);
                    RecordTrace(TraceKind.NetworkError, e.StackTrace);
                }
            }
        }

        /// <summary>
        /// Trace an error which is of no consequence, and should not normally be considered a
        /// problem. Implicit Kind is Error|Detail
        /// </summary>
        /// <param name="e">An exception to dump</param>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void UninterestingError(Exception e, string format, params object[] args)
        {
            if (IsEnabled(TraceKind.Error | TraceKind.Detail))
            {
                RecordTrace(TraceKind.Error | TraceKind.Detail, _Format(format, args));
                if (e != null)
                {
                    RecordTrace(TraceKind.Error | TraceKind.Detail, e.Message);
                    RecordTrace(TraceKind.Error | TraceKind.Detail, e.StackTrace);
                }
            }
        }

        public void AssertFailed(string format, params object[] args)
        {
            // always enabled

            RecordTrace(TraceKind.None, _Format(format, args));

            try
            {
                throw new Exception("ASSERTFAILED");
            }
            catch (Exception e)
            {
                RecordTrace(TraceKind.None, e.Message);
                RecordTrace(TraceKind.None, e.StackTrace);
            }

        }

        /// <summary>
        /// Trace a block of code with a message at the head and an indent
        /// Usually called with using([tracer].Block(...))
        /// Implicit kind is EntryExit
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        /// <returns>IDisposable representing block</returns>
        public IDisposable Block(string format, params object[] args)
        {
            if (IsEnabled(TraceKind.EntryExit))
            {
                string msg = _Format(format, args);
                RecordTrace(TraceKind.EntryExit, msg);

                if (IsEnabled(TraceKind.Timing | TraceKind.BlockExit | TraceKind.BlockEntry))
                {
                    RecordTrace(TraceKind.BlockEntry, "{");
                    return new IndentBlock(this, DateTime.Now);
                }
                else if (IsEnabled(TraceKind.BlockExit | TraceKind.BlockEntry))
                {
                    RecordTrace(TraceKind.BlockEntry, "{");
                    return new IndentBlock(this, "}");
                }
                else
                    return new IndentBlock(null, null);
            }
            else
            {
                return NullDisposableInstance;
            }
        }

        /// <summary>
        /// Trace a block of code with a message at the head and an indent
        /// Usually called with using([tracer].Block(...))
        /// Kind is specified explicitly
        /// </summary>
        /// <param name="opt"></param>
        /// <param name="format"></param>
        /// <param name="args"></param>
        /// <returns>IDisposable representing block</returns>
        public IDisposable Block(TraceKind opt, string format, params object[] args)
        {
            opt = opt | TraceKind.EntryExit;
            if (IsEnabled(opt))
            {
                string msg = _Format(format, args);
                RecordTrace(opt, msg);

                if (IsEnabled(opt | TraceKind.BlockExit | TraceKind.BlockEntry | TraceKind.Timing))
                {
                    RecordTrace(TraceKind.BlockEntry, "{");
                    return new IndentBlock(this, DateTime.Now);
                }
                else if (IsEnabled(opt | TraceKind.BlockExit))
                {
                    RecordTrace(TraceKind.BlockEntry, "{");
                    return new IndentBlock(this, "}");
                }
                else
                {
                    return new IndentBlock(null, null);
                }
            }
            else
            {
                return NullDisposableInstance;
            }
        }

        #endregion


        #region static
        public static readonly Guid TraceProviderGuid = new Guid("ABCCE31F-3350-4EFF-CA4E-C9A5BE4F4082");
        public static readonly Guid AOLTraceProviderGuid = new Guid("23498DFD-FBFD-4458-B989-446EBCE55DF9");
        private static readonly IDisposable NullDisposableInstance = new NullDisposable();
        private static ITraceWriter logfile;
        private static Dictionary<string, TraceModule> modules;
        private readonly object lockObject = new object();

        public static long MaxLogLengthAtStartup = 5 * 1024 * 1024;


        /// <summary>
        /// Static method to look for a config file. All tracing is disabled if file is not found.
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="defaultTracing">Tracing to enable if no log.conf found</param>
        /// <returns></returns>
        private static bool ReadConfig(string dir, string filename, string defaultTracing)
        {
            try
            {
                string path = Path.Combine(dir, filename);
                if (!File.Exists(path))
                    path = Path.Combine(dir, filename + ".txt");

                TextReader r;

                if (File.Exists(path))
                {
                    r = new StreamReader(path);
                }
                else if (!String.IsNullOrEmpty(defaultTracing))
                {
                    r = new StringReader(defaultTracing);
                }
                else
                {
                    return false;
                }

                return ReadConfig(r);
            }
            catch (Exception e)
            {
                return false;
            }
        }

        private static bool ReadConfig(TextReader r)
        {
            try
            {
                using (r)
                {
                    for (; ; )
                    {
                        string line = r.ReadLine();

                        if (line == null)
                            break;

                        try
                        {
                            line = line.Trim();

                            if (line.Length == 0)
                                continue;

                            if (line[0] == '#')
                                continue;

                            string[] bits = line.Split(' ', '=', '|', ':', ',', ';');

                            if (bits.Length == 1)
                            {
                                if ("*deletefile".Equals(bits[0], StringComparison.OrdinalIgnoreCase))
                                {
                                    resetFile = true;
                                }
                                else if ("*verbose".Equals(bits[0], StringComparison.OrdinalIgnoreCase))
                                {
                                    FullTraceKind = true;
                                }
                                else if ("*milliseconds".Equals(bits[0], StringComparison.OrdinalIgnoreCase))
                                {
                                    DetailedTiming = true;
                                }
                            }

                            if (bits.Length < 2)
                                continue;

                            string modulename = bits[0].ToLower();
                            TraceModule module;

                            if (modulename == "global")
                                module = null;
                            else
                                module = LookupModule(modulename);

                            for (int i = 1; i < bits.Length; i++)
                            {
                                string o = bits[i].Trim();
                                if (o == "")
                                    continue;

                                bool neg = false;

                                if ((o[0] == '!') || (o[0] == '-') || (o[0] == '~'))
                                {
                                    neg = true;
                                    o = o.Substring(1);
                                }

                                try
                                {
                                    TraceKind nextopt = (TraceKind)Enum.Parse(typeof(TraceKind), o, true);

                                    if (neg)
                                    {
                                        // negate. Disable this setting.

                                        // if module is null, we disable for all registered modules
                                        if (module == null)
                                        {
                                            foreach (TraceModule m in modules.Values)
                                                m.Mode = m.Mode & ~nextopt;
                                        }
                                        else
                                        {
                                            module.Mode = module.Mode & ~nextopt;
                                        }
                                    }
                                    else
                                    {
                                        // if module Is null add this to all registered modules
                                        if (module == null)
                                        {
                                            foreach (TraceModule m in modules.Values)
                                                m.Mode = m.Mode | nextopt;
                                        }
                                        else
                                        {
                                            module.Mode = module.Mode | nextopt;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Tracer.Misc.Error(ex, "Failed: ");
                                }

                            }
                        }
                        catch (Exception ex)
                        { Tracer.Misc.Error(ex, "Failed: "); }
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void SetOption(string module, TraceKind opt)
        {
            module = module.ToLower();

            LookupModule(module).Mode = opt;

        }

        public static TraceModule LookupModule(string name)
        {
            name = name.ToLower();

            if (modules == null)
                modules = new Dictionary<string, TraceModule>();

            TraceModule t;

            if (!modules.TryGetValue(name, out t))
            {
                t = new TraceModule(name);
                modules[name] = t;
            }

            return t;
        }

        public static void AddModule(string name, TraceModule module)
        {
            name = name.ToLower();

            if (modules == null)
                modules = new Dictionary<string, TraceModule>();

            TraceModule t;

            if (modules.TryGetValue(name, out t))
            {
                module.Mode = t.Mode;
            }

            modules[name] = module;
        }

        private static bool resetFile = false;
        private static bool DetailedTiming = false;
        private static bool FullTraceKind = false;

        /// <summary>
        /// Log the 'header'
        /// </summary>
        /// <param name="args"></param>
        private static void WriteLog(IList<string> args)
        {
            WriteToLog("==============================================");

            System.Reflection.AssemblyName an = System.Reflection.Assembly.GetEntryAssembly().GetName();

            // LCM-7970: Please do not modify this trace it is consumed by Seamless Log Analyzer Tool.
            WriteToLog("GlobalClientSessionInfo: " + an.Name + " " + "CWAVersion=" + an.Version);
            WriteToLog("");

            WriteToLog("Starting uct:" + DateTime.Now.ToUniversalTime() + " local: " + DateTime.Now);
            if (args != null)
            {
                if (args.Count == 0)
                {
                    WriteToLog(" (no arguments)");
                }
                else
                {
                    foreach (string a in args)
                        WriteToLog(" " + a);
                }
            }
            else
            {
                WriteToLog(" (null arguments)");
            }

            WriteToLog(String.Format("Usage: *deletefile={0} *milliseconds={1} *verbose={2}", resetFile, FullTraceKind,
                                     DetailedTiming));
            WriteToLog("==============================================");
        }

        /// <summary>
        /// Called early in the traced program. This checks to see if tracing is enabled, and if it is
        /// dumps a summary of the calling parameters for the program + the date and time it was started
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="name"></param>
        /// <param name="args"></param>
        public static void Init(string dir, string confname, string name, IList<string> args, string defaultTracing)
        {
            logfile = null;

            ProblemReporter.Init(Path.Combine(dir, "problem.txt"));

            // create a log file if 'traditional' logging is enabled

            if (ReadConfig(dir, confname, defaultTracing))
            {
                try
                {
                    Directory.CreateDirectory(dir);

                    string path = dir + "\\" + name + ".txt";

                    try
                    {
                        FileInfo info = new FileInfo(path);
                        if ((info.Exists) && ((resetFile) || (info.Length > MaxLogLengthAtStartup)))
                        {
                            info.Delete();
                        }
                    }
                    catch (Exception)
                    {
                        // silently fail
                    }

                    logfile = CreateTraceWriter(path);
                }

                catch (Exception ee)
                {
                    logfile = null;
                    Tracer.Misc.RecordTrace(TraceKind.None, "", "Failed to open Log file" + ee.Message);
                }
            }

            // start CDF tracing as well if that is enabled
            InitCDFTracing(name);

            // log a 'header'. If neither the logfile not CDF is enabled, this is a 'waste' but no big deal.
            WriteLog(args);

            if (logfile != null)
                logfile.Flush();

        }

        private static void OnCDFTracingChanged(CdfTrace sender)
        {

            if (sender.Enabled)// will get turned on externally
            {
                TraceKind kind = TraceKind.None;

                // cdf is a global enabler. Not per category

                if (sender.IsEnabled(CdfTrace.Category.Entry))
                {
                    kind |= TraceKind.EntryExit | TraceKind.BlockEntry | TraceKind.BlockExit;
                }

                if (sender.IsEnabled(CdfTrace.Category.Error))
                {
                    kind |= TraceKind.Error | TraceKind.NetworkError;
                }

                if (sender.IsEnabled(CdfTrace.Category.Performance))
                {
                    kind |= TraceKind.Timing;
                }

                if (sender.IsEnabled(CdfTrace.Category.Info))
                {
                    kind |= TraceKind.Message;

                    if (sender.Level <= CdfTrace.SeverityLevel.InformationalDetailed)
                    {
                        kind |= TraceKind.Detail;
                    }
                }

                // if we found any CDF tracing, enable tracing for all modules, else turn it off
                if (kind == TraceKind.None)
                {
                    sender.DisableTracing();
                }
                else
                {
                    foreach (var m in modules.Values)
                    {
                        m.Mode |= (kind | TraceKind.Continuos);
                    }
                }
            }
        }


        private static void InitCDFTracing(string name)
        {
            try
            {
                cdf = new CdfTrace(TraceProviderGuid, "Receiver_" + name, OnCDFTracingChanged);
                AOLcdf = new CdfTrace(AOLTraceProviderGuid, "AOLReceiver_" + name, OnCDFTracingChanged);
            }
            catch (Exception ex)
            {
                cdf = null;
                AOLcdf = null;
                WriteToLog("CDF initialization failed " + ex.Message);
                throw;
            }
        }

        private static void DisposeCDFTracing()
        {
            if (cdf != null)
            {
                cdf.Dispose();
                cdf = null;
            }
            if (AOLcdf != null)
            {
                AOLcdf.Dispose();
                AOLcdf = null;
            }
            WriteToLog("CDF stopped ");
        }

        private static CdfTrace cdf;
        private static CdfTrace AOLcdf;

        private static ITraceWriter CreateTraceWriter(string path)
        {
            ITraceWriter traceWriter = new FileTraceWriter(path, true, Encoding.ASCII, 4096);
            AddDotNetTraceWriterIfDebug(ref traceWriter);
            return traceWriter;
        }

        [Conditional("DEBUG")]
        private static void AddDotNetTraceWriterIfDebug(ref ITraceWriter traceWriter)
        {
            // todo
        }

        /// <summary>
        /// Called when tracing is complete to close any open log file.
        /// Effectively optional as program exit will normally close the file.
        /// </summary>
        public static void Exit()
        {
            DisposeCDFTracing();
            if (logfile != null)
            {
                logfile.Flush();
                logfile.Close();
                logfile = null;
            }
        }

        [ThreadStatic]
        static internal string indent;
        private static int threadid = 100;

        internal static void Indent()
        {
            string i = TraceModule.indent;

            if (i == null)
                i = "  ";
            else
                i = i + "  ";

            TraceModule.indent = i;
        }

        internal static void Outdent()
        {
            string i = TraceModule.indent;

            if ((i != null) && (i.Length > 2))
                i = i.Substring(2);
            else
                i = null;

            TraceModule.indent = i;
        }

        private static string TraceKindToString(TraceKind k)
        {
            if (FullTraceKind)
            {
                return String.Format("{0,20}", k);
            }
            else
            {
                switch (k & ~TraceKind.BlockEntry)
                {

                    case TraceKind.EntryExit:
                        return "=";
                    case TraceKind.BlockEntry:
                        return ">";
                    case TraceKind.BlockExit:
                        return "<";
                    case TraceKind.Message:
                        return " ";
                    case TraceKind.Detail:
                        return "D";
                    case TraceKind.Continuos:
                        return "C";
                    case TraceKind.Error:
                        return "E";
                    case TraceKind.Sensitive:
                        return "*";
                    case TraceKind.Error | TraceKind.Detail:
                        return "e";
                    case TraceKind.NetworkError:
                        return "N";
                    case TraceKind.SysCall:
                        return "S";
                    case TraceKind.Debug:
                        return "d";
                    default:
                        if (k == TraceKind.BlockEntry)
                            return ">";
                        else if (k == TraceKind.None)
                        {
                            return "A";
                        }
                        else
                            return "?";
                }
            }
        }


        private static readonly char[] CR = { '\n', '\r' };

        internal static void WriteToLog(string rawmessage)
        {
            if (logfile != null) logfile.WriteLine(rawmessage);

            if (cdf != null) cdf.TraceMsg(rawmessage);
        }

        private void WriteToLog(TraceKind kind, string mname, string message)
        {
            DateTime now = default(DateTime);

            if (logfile != null)
            {
                StringBuilder b = new StringBuilder();

                if (now == default(DateTime))
                    now = DateTime.Now;

                b.Append(now.Day);
                b.Append('/');
                b.Append(now.ToLongTimeString());

                if (DetailedTiming)
                {
                    b.Append(String.Format(".{0:000}", now.Millisecond));
                }

                b.Append(' ');

                int l = mname.Length;

                if (l == 8)
                {
                    // common case - test first
                    b.Append(mname);
                }
                else if (l > 8)
                {
                    b.Append(mname, 0, 8);
                }
                else
                {
                    b.Append(mname);
                    b.Append(' ', 8 - l);
                }

                b.Append(' ');
                b.Append(TraceKindToString(kind));
                b.Append(' ');

                string tn = Thread.CurrentThread.Name;
                if (tn == null)
                {
                    lock (lockObject)
                    {
                        tn = "T" + threadid++;
                        Thread.CurrentThread.Name = tn;
                    }
                }

                l = tn.Length;

                if (l > 8)
                {
                    b.Append(tn, 0, 8);
                }
                else
                {
                    b.Append(tn);

                    if (l < 8)
                    {
                        b.Append(' ', 8 - l);
                    }
                }

                b.Append(':');

                // care reading indent as we have no thread lock
                string i = TraceModule.indent;

                if (i != null)
                    b.Append(i);

                b.Append(message);

                logfile.WriteLine(b.ToString());

                logfile.Flush();
            }
        }

        /// <summary>
        /// Low level routine to record a trace message without any checking to see if it is enabled.
        /// </summary>
        /// <param name="kind"></param>
        /// <param name="message"></param>
        protected void RecordTrace(TraceKind kind, string message)
        {
            if (message != null)
            {
                if (message.IndexOfAny(CR) == -1)
                {
                    RecordTrace(kind, moduleNamePrettyPrint, message);
                }
                else
                {
                    string[] lines = message.Split(CR);

                    foreach (string line in lines)
                    {
                        if (!String.IsNullOrEmpty(line))
                            RecordTrace(kind, moduleNamePrettyPrint, line);
                    }
                }
            }
        }

        protected void RecordTrace(TraceKind kind, CdfTrace.Category category, CdfTrace.SeverityLevel level, string message)
        {
            if (cdf != null && cdf.Enabled)
                cdf.Trace(moduleNamePrettyPrint, category, level, message);

            WriteToLog(kind, moduleNamePrettyPrint, message);
        }

        protected void AOLRecordTrace(TraceKind kind, CdfTrace.Category category, CdfTrace.SeverityLevel level, string message)
        {
            if (AOLcdf != null && AOLcdf.Enabled)
                AOLcdf.Trace(moduleNamePrettyPrint, category, level, message);
        }

        protected CdfTrace.Category GetCategory(TraceKind kind)
        {
            CdfTrace.Category category;
            if ((kind & TraceKind.Error) == TraceKind.Error)
            {
                category = CdfTrace.Category.Error;
            }
            else if ((kind & (TraceKind.BlockEntry | TraceKind.BlockExit | TraceKind.EntryExit)) != 0)
            {
                category = CdfTrace.Category.Entry;
            }
            else if ((kind & TraceKind.Timing) == TraceKind.Timing)
            {
                category = CdfTrace.Category.Performance;
            }
            else
            {
                category = CdfTrace.Category.Info;
            }
            return category;
        }

        protected CdfTrace.SeverityLevel GetSeverityLevel(TraceKind kind)
        {
            return (kind & TraceKind.Detail) == TraceKind.Detail ? CdfTrace.SeverityLevel.InformationalDetailed : CdfTrace.SeverityLevel.Important;
        }

        protected bool IsAOLogEnabledForCurrentTraceModule(string traceModuleName)
        {
            foreach (string name in Tracer.AOLogEnabledTraceModuleList)
            {
                string moduleName = name.ToLower();
                moduleName = (moduleName + "        ").Substring(0, 8); //Refer TraceModule(string name) constructor
                if (moduleName == traceModuleName)
                    return true;
            }
            return false;
        }

        protected void RecordTrace(TraceKind kind, string mname, string message)
        {
            DateTime now = default(DateTime);

            if ((kind & TraceKind.Call) == TraceKind.Call)
            {
                if (TraceRegistry.EnableProblemReporter)
                {
                    StringBuilder b = new StringBuilder();

                    now = DateTime.Now;

                    b.Append(now.ToLongDateString());
                    b.Append('/');
                    b.Append(now.ToLongTimeString());
                    b.Append(' ');
                    b.Append(message);

                    ProblemReporter.Log(b.ToString());

                    if ((kind & TraceKind.Error) == TraceKind.Error)
                        ProblemReporter.NoteError();
                }

                // are we really tracing this message, or is it just a special case for problem reporter
                if ((Mode & kind) == TraceKind.None)
                    return;
            }

            CdfTrace.SeverityLevel severity = GetSeverityLevel(kind);
            CdfTrace.Category category = GetCategory(kind);

            if ((cdf != null) && (cdf.Enabled))
                cdf.Trace(mname, category, severity, message);

            if (AOLcdf != null && AOLcdf.Enabled && IsAOLogEnabledForCurrentTraceModule(mname))
                AOLcdf.Trace(mname, category, severity, message);

            // Log the message to file.
            WriteToLog(kind, mname, message);
        }

        /// <summary>
        /// Masks sensitive data, while retaining some clues to what the data
        /// actually is.
        /// 
        /// Example:  "This is my sensitive data" becomes 
        ///           "T.......................a"
        /// </summary>
        /// <param name="data">The data to mask
        /// NOTE: Data less that three characters will not be masked
        /// </param>
        public static string MaskData(string data)
        {
            // From CDF.net
            string rtnVal = "";

            // if the data is 3+ characters we can put dots in the middle
            if (data.Length > 2)
            {
                string dots = "";

                // construct the string of dots equal to the length of the
                // original string minus the first and last character
                for (int x = 0; x < data.Length - 2; x++)
                {
                    dots += ".";
                }

                // create the new string
                rtnVal = string.Format("{0}{1}{2}", data[0], dots, data[data.Length - 1]);
            }
            else
            {
                // 2 or fewer characters, not much we can do
                rtnVal = data;
            }

            return rtnVal;
        }

        /// <summary>
        /// Private routine to format a message with optional args. Will catch any exception this
        /// generates so it can be used safely.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        /// <returns>formatted string</returns>
        private static string _Format(string format, object[] args)
        {
            if ((args == null) || (args.Length == 0))
                return format;

#if DEBUG
            if ((args.Length > 0) && (format.IndexOf('{') == -1))
            {
                // probability of forgotton format
                System.Diagnostics.Debugger.Break();
            }
#endif

            try
            {
                return String.Format(format, args);
            }
            catch (Exception e)
            {
                return format + " [Format Exception:" + e.Message + "]";
            }
        }
        #endregion


        #region Modules


        #endregion

        public void Trace(TraceKind traceKind, XmlDocument doc, string message)
        {
            if (IsEnabled(traceKind))
            {
                using (Block(message))
                {
                    Stream stream = new MemoryStream();
                    XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings()
                    {
                        Indent = true,
                    });
                    doc.WriteTo(writer);
                    stream.Seek(0, SeekOrigin.Begin);

                    TextReader reader = new StreamReader(stream);

                    for (; ; )
                    {
                        string line = reader.ReadLine();
                        if (line == null)
                            break;
                        RecordTrace(traceKind, "XML     ", line);
                    }
                }
            }
        }

    }

    /// <summary>
    /// Static list of all trace modules we use. Trace statements should call Tracer.xyz.Trace()
    /// </summary>
    public static class Tracer
    {
        // NB names should be 8 chars or less, as that is all that is shown in trace log

        public static readonly TraceModule JS = TraceModule.LookupModule("JS");
        public static readonly TraceModule Web = TraceModule.LookupModule("Web");
        public static readonly TraceModule Jumplist = TraceModule.LookupModule("Jumplist");
        public static readonly TraceModule StartmenuActions = TraceModule.LookupModule("Jumplist");
        public static readonly TraceModule OnPrem = TraceModule.LookupModule("Web.OnPrem");
        public static readonly TraceModule Cloud = TraceModule.LookupModule("Web.Cloud");
        public static readonly TraceModule CLSyncWeb = TraceModule.LookupModule("CLSync");
        public static readonly TraceModule UI = TraceModule.LookupModule("UI");
        public static readonly TraceModule DragDrop = TraceModule.LookupModule("DragDrop");
        public static readonly TraceModule Controller = TraceModule.LookupModule("Control");
        public static readonly TraceModule Rade = TraceModule.LookupModule("Rade");
        public static readonly TraceModule Misc = TraceModule.LookupModule("Misc");
        public static readonly TraceModule Install = TraceModule.LookupModule("Install");
        public static readonly TraceModule PNA = TraceModule.LookupModule("PNA");
        public static readonly TraceModule DServices = TraceModule.LookupModule("DService");
        public static readonly TraceModule AuthMan = TraceModule.LookupModule("AuthMan");
        public static readonly TraceModule Launch = TraceModule.LookupModule("Launch");
        public static readonly TraceModule Receiver = TraceModule.LookupModule("Receiver");
        public static readonly TraceModule G2M = TraceModule.LookupModule("G2M");
        public static readonly TraceModule FTA = TraceModule.LookupModule("FTA");
        public static readonly TraceModule Sync = TraceModule.LookupModule("Sync");
        public static readonly TraceModule VTP = TraceModule.LookupModule("VTP");
        public static readonly TraceModule Icon = TraceModule.LookupModule("Icon");
        public static readonly TraceModule BG = TraceModule.LookupModule("BG");
        public static readonly TraceModule ConfigMgr = TraceModule.LookupModule("ConfigMgr");
        public static readonly TraceModule CleanUp = TraceModule.LookupModule("CleanUp");
        public static readonly TraceModule PassthroughClient = TraceModule.LookupModule("PassthroughClient");
        public static readonly TraceModule IWS = TraceModule.LookupModule("IWS");
        public static readonly TraceModule AML = TraceModule.LookupModule("AML");
        public static readonly TraceModule AMLJS = TraceModule.LookupModule("AML.JS");
        public static readonly TraceModule Browsers = TraceModule.LookupModule("Browsers");
        public static readonly TraceModule Handlers = TraceModule.LookupModule("Browsers.Handlers");
        public static readonly TraceModule ConnectionLeasing = TraceModule.LookupModule("ConnLease");
        public static readonly TraceModule AccountConfig = TraceModule.LookupModule("AccountConfig");
        public static readonly TraceModule AccountConfigJS = TraceModule.LookupModule("AccountConfig.JS");
        public static readonly TraceModule WebModules = TraceModule.LookupModule("WebModules");
        public static readonly TraceModule BaseClassLibrary = TraceModule.LookupModule("BCLib");
        public static readonly TraceModule CustomPortal = TraceModule.LookupModule("CustomPortal");
        public static readonly TraceModule AccountMigration = TraceModule.LookupModule("AccountMigration");
        public static readonly TraceModule StoreBrowse = TraceModule.LookupModule("StoreBrowse");

        public static List<string> AOLogEnabledTraceModuleList = new List<string>
        {
            "ConnLease", "JS", "Web", "Web.OnPrem", "Web.Cloud", "UI", "DragDrop", "Control",
            "Misc","PNA","DService","AuthMan","Launch","Receiver","G2M","FTA","Sync","VTP","Icon",
            "BG","ConfigMgr","CleanUp","PassthroughClient","IWS","AML","AML.JS","Browsers","Browsers.Handlers",
            "AccountConfig","WebModules","BCLib", "CustomPortal", "AccountMigration", "StoreBrowse"
        };
    }

    public class ProblemReporter
    {
        private IList<string> items = new List<string>();
        private int suppressCount;
        private bool SeenError;
        private static readonly LocalDataStoreSlot slot;
        private static string path;
        private readonly object lockObject = new object();

        public static void EnableCallComplete()
        {
            if (TraceRegistry.EnableProblemReporter)
            {
                ProblemReporter p = ThreadItem;
                p.suppressCount--;
                p.SeenError = false; // clear any error flag
            }
        }

        public static void SuppressCallComplete()
        {
            if (TraceRegistry.EnableProblemReporter)
                ThreadItem.suppressCount++;
        }

        internal static void Init(string path)
        {
            ProblemReporter.path = path;
        }

        static ProblemReporter()
        {
            if (TraceRegistry.EnableProblemReporter)
                slot = Thread.AllocateDataSlot();
        }

        internal static void Log(string line)
        {
            if (TraceRegistry.EnableProblemReporter)
            {
                IList<string> s = ThreadItem.items;
                if (s.Count == 0)
                    s.Add(String.Format("{0}-----------------------", DateTime.Now.ToShortTimeString()));
                s.Add(line);
            }
        }


        public static void NoteError()
        {
            if (TraceRegistry.EnableProblemReporter)
            {
                ProblemReporter p = ThreadItem;
                p.SeenError = true;
            }
        }

        public static void CallComplete()
        {
            if (!TraceRegistry.EnableProblemReporter)
                return;
            try
            {
                ProblemReporter self = ThreadItem;

                bool afterError = self.SeenError;

                if (self.suppressCount == 0)
                {
                    if ((afterError) || (TraceRegistry.ReportAllLaunches))
                        self.FlushToLog();

                    self.items.Clear();
                }
            }
            catch (Exception e)
            {
                Tracer.Misc.Error(e, "exception in ProblemReporter");
            }
        }

        private void FlushToLog()
        {
            if (items.Count == 0)
                return;

            foreach (var m in items)
            {
                TraceModule.WriteToLog(m);
            }

            lock (lockObject)
            {
                ITraceWriter traceWriter = new FileTraceWriter(path, true, Encoding.ASCII, 4096);

                System.Reflection.AssemblyName an = System.Reflection.Assembly.GetEntryAssembly().GetName();

                traceWriter.WriteLine(">>> " + an);

                foreach (var m in items)
                {
                    traceWriter.WriteLine(m);
                }

                traceWriter.Close();
            }
        }

        private static ProblemReporter ThreadItem
        {
            get
            {
                ProblemReporter p = (ProblemReporter)Thread.GetData(slot);
                if (p == null)
                {
                    p = new ProblemReporter();
                    Thread.SetData(slot, p);
                }

                return p;
            }
        }

    }

#if DEMOSUPPORT
    public static class Demo
    {
        public static bool DemoTweaks = false; // turn of UI that can demo badly (eg hourglass)
        public static bool Demo_NoPoll = false; // no auto-poll
        public static bool DemoFarm = false; // Tweak results from Showcase so they look better
        public static bool DemoRecommended = false;
      
        public static bool SkipResource(string rid)
        {
            if (!DemoFarm)
                return false;

            if ((rid != null) && (rid.IndexOf("Change Default Printer") != -1))
                return true;

            return false;
        }

        public static string Demo_ShortName(string name)
        {
            if(!DemoFarm)
                return name;

            if (name == null)
                return null;

            name = name.Replace(" - Hosted", "");
            name = name.Replace(" - Streamed", "");
            return name;
        }

        public static string Demo_ShortFolder(string folder)
        {
            if(!DemoFarm)
                return folder;

            if (folder == null)
                return null;

            folder = folder.Replace(@"Hosted Apps", "");
            folder = folder.Replace(@"Streamed Apps", "");

            folder = folder.Trim(' ', '\\');

            int i = folder.IndexOf('\\');

            if (i != -1)
            {
                folder = folder.Substring(0, i);
            }

            return folder.Trim(' ', '\\');
        }

    }
#endif
}
