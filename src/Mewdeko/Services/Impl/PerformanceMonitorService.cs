using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Serilog;

namespace Mewdeko.Services.Impl
{
    /// <summary>
    /// Service that automatically monitors performance across entire namespaces and assemblies.
    /// </summary>
    public class PerformanceMonitorService : INService
    {
        private readonly ConcurrentDictionary<string, MethodPerformanceData> methodPerformanceData = new();
        private bool initialized;

        /// <summary>
        /// Initializes a new instance of the <see cref="PerformanceMonitorService"/> class.
        /// </summary>
        public PerformanceMonitorService()
        {
            Process.GetCurrentProcess();

            // Log performance data every 5 minutes
            new Timer(LogPerformanceData, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        /// <summary>
        /// Initializes performance monitoring for the specified namespace in the given assembly.
        /// </summary>
        /// <param name="assembly">The assembly to monitor.</param>
        /// <param name="namespaceToMonitor">The namespace to monitor (can be a parent namespace).</param>
        public void Initialize(Assembly assembly, string namespaceToMonitor)
        {
            if (initialized)
                return;

            Log.Information($"Initializing performance monitoring for namespace {namespaceToMonitor} in assembly {assembly.GetName().Name}");

            try
            {
                // Find all types in the specified namespace
                var types = assembly.GetTypes()
                    .Where(type => type.Namespace != null &&
                                  type.Namespace.StartsWith(namespaceToMonitor) &&
                                  !type.IsAbstract &&
                                  !type.IsInterface &&
                                  type.IsClass)
                    .ToList();

                Log.Information($"Found {types.Count} types to monitor in namespace {namespaceToMonitor}");

                // Register for dynamic proxy creation
                foreach (var type in types)
                {
                    InstrumentTypeWithReflection(type);
                }

                initialized = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error initializing performance monitoring for namespace {namespaceToMonitor}");
            }
        }

        /// <summary>
        /// Uses reflection to hook into methods for performance monitoring.
        /// Note: This is a basic implementation. For production use, consider
        /// using library-based interception like Castle DynamicProxy.
        /// </summary>
        /// <param name="type">The type to instrument.</param>
        private void InstrumentTypeWithReflection(Type type)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                        BindingFlags.Instance | BindingFlags.Static |
                                        BindingFlags.DeclaredOnly)
                            .Where(m => !m.IsSpecialName && // Skip property accessors
                                       !m.IsConstructor &&
                                       !m.GetCustomAttributes(typeof(CompilerGeneratedAttribute), true).Any())
                            .ToList();

            Log.Debug($"Instrumenting {methods.Count} methods in type {type.FullName}");

            // Record the type and its methods for monitoring
            foreach (var method in methods)
            {
                var methodKey = $"{type.FullName}.{method.Name}";
                methodPerformanceData.TryAdd(methodKey, new MethodPerformanceData(methodKey));
            }
        }

        /// <summary>
        /// Manually tracks method execution for methods that can't be auto-instrumented.
        /// </summary>
        /// <param name="methodName">The name of the method.</param>
        /// <param name="executionTime">The execution time.</param>
        public void RecordMethodExecution(string methodName, TimeSpan executionTime)
        {
            methodPerformanceData.AddOrUpdate(
                methodName,
                _ => new MethodPerformanceData(methodName, executionTime),
                (_, existing) =>
                {
                    existing.AddExecution(executionTime);
                    return existing;
                });
        }

        /// <summary>
        /// Creates a performance measurement tracker for manually instrumenting methods.
        /// </summary>
        /// <param name="methodName">The name of the method to track.</param>
        /// <returns>An IDisposable that will record the execution time when disposed.</returns>
        public IDisposable Measure(string methodName)
        {
            return new MethodPerformanceTracker(this, methodName);
        }

        /// <summary>
        /// Gets the top CPU-intensive methods based on execution time.
        /// </summary>
        /// <param name="count">The number of methods to return.</param>
        /// <returns>An array of method performance data ordered by execution time.</returns>
        public MethodPerformanceData[] GetTopCpuMethods(int count = 20)
        {
            return methodPerformanceData.Values
                .Where(x => x.CallCount > 0)
                .OrderByDescending(x => x.TotalExecutionTime.TotalMilliseconds / x.CallCount)
                .Take(count)
                .ToArray();
        }

        /// <summary>
        /// Gets the most frequently called methods.
        /// </summary>
        /// <param name="count">The number of methods to return.</param>
        /// <returns>An array of method performance data ordered by call count.</returns>
        public MethodPerformanceData[] GetMostCalledMethods(int count = 20)
        {
            return methodPerformanceData.Values
                .OrderByDescending(x => x.CallCount)
                .Take(count)
                .ToArray();
        }

        /// <summary>
        /// Clears all collected performance data.
        /// </summary>
        public void ClearPerformanceData()
        {
            foreach (var entry in methodPerformanceData)
            {
                entry.Value.Reset();
            }
        }

        /// <summary>
        /// Logs the current performance data to the configured logger.
        /// </summary>
        /// <param name="state">The state object passed to the timer callback.</param>
        private void LogPerformanceData(object state)
        {
            var activeMethods = methodPerformanceData.Values.Where(x => x.CallCount > 0).ToList();
            if (activeMethods.Count == 0)
                return;

            var sb = new StringBuilder("Performance monitoring - Top CPU intensive methods:\n");

            var topMethods = activeMethods
                .OrderByDescending(x => x.TotalExecutionTime.TotalMilliseconds / x.CallCount)
                .Take(10);

            foreach (var method in topMethods)
            {
                sb.AppendLine($"{method.MethodName}: {method.TotalExecutionTime.TotalMilliseconds:N2}ms total, " +
                             $"{method.TotalExecutionTime.TotalMilliseconds / method.CallCount:N2}ms avg, " +
                             $"called {method.CallCount} times");
            }

            Log.Information(sb.ToString());
        }

        /// <summary>
        /// Represents performance data for a single method.
        /// </summary>
        public class MethodPerformanceData
        {
            /// <summary>
            /// Gets the name of the method being tracked.
            /// </summary>
            public string MethodName { get; }

            /// <summary>
            /// Gets the number of times this method has been called.
            /// </summary>
            public int CallCount { get; private set; }

            /// <summary>
            /// Gets the total execution time across all calls to this method.
            /// </summary>
            public TimeSpan TotalExecutionTime { get; private set; }

            /// <summary>
            /// Gets the timestamp of the last execution of this method.
            /// </summary>
            public DateTime LastExecuted { get; private set; }

            /// <summary>
            /// Gets the maximum execution time observed for this method.
            /// </summary>
            public TimeSpan MaxExecutionTime { get; private set; }

            /// <summary>
            /// Gets the minimum execution time observed for this method.
            /// </summary>
            public TimeSpan MinExecutionTime { get; private set; }

            /// <summary>
            /// Gets the average execution time per call in milliseconds.
            /// </summary>
            public double AvgExecutionTime => CallCount > 0 ?
                TotalExecutionTime.TotalMilliseconds / CallCount : 0;

            /// <summary>
            /// Initializes a new instance of the <see cref="MethodPerformanceData"/> class.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            public MethodPerformanceData(string methodName)
            {
                MethodName = methodName;
                CallCount = 0;
                TotalExecutionTime = TimeSpan.Zero;
                MaxExecutionTime = TimeSpan.Zero;
                MinExecutionTime = TimeSpan.MaxValue;
                LastExecuted = DateTime.MinValue;
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="MethodPerformanceData"/> class with an initial execution.
            /// </summary>
            /// <param name="methodName">The name of the method.</param>
            /// <param name="initialExecutionTime">The execution time of the first call.</param>
            public MethodPerformanceData(string methodName, TimeSpan initialExecutionTime) : this(methodName)
            {
                AddExecution(initialExecutionTime);
            }

            /// <summary>
            /// Records an additional execution of this method.
            /// </summary>
            /// <param name="executionTime">The execution time of the additional call.</param>
            public void AddExecution(TimeSpan executionTime)
            {
                var callCount = CallCount;
                Interlocked.Increment(ref callCount);
                TotalExecutionTime += executionTime;
                LastExecuted = DateTime.UtcNow;

                // Update min/max stats
                if (executionTime > MaxExecutionTime)
                    MaxExecutionTime = executionTime;

                if (executionTime < MinExecutionTime)
                    MinExecutionTime = executionTime;
            }

            /// <summary>
            /// Resets the performance data.
            /// </summary>
            public void Reset()
            {
                CallCount = 0;
                TotalExecutionTime = TimeSpan.Zero;
                MaxExecutionTime = TimeSpan.Zero;
                MinExecutionTime = TimeSpan.MaxValue;
                LastExecuted = DateTime.MinValue;
            }
        }

        /// <summary>
        /// Tracks the execution time of a method and reports it when disposed.
        /// </summary>
        private class MethodPerformanceTracker : IDisposable
        {
            private readonly PerformanceMonitorService service;
            private readonly string methodName;
            private readonly Stopwatch stopwatch;

            /// <summary>
            /// Initializes a new instance of the <see cref="MethodPerformanceTracker"/> class.
            /// </summary>
            /// <param name="service">The performance monitor service.</param>
            /// <param name="methodName">The name of the method being tracked.</param>
            public MethodPerformanceTracker(PerformanceMonitorService service, string methodName)
            {
                this.service = service;
                this.methodName = methodName;
                stopwatch = Stopwatch.StartNew();
            }

            /// <summary>
            /// Records the method execution time when disposed.
            /// </summary>
            public void Dispose()
            {
                stopwatch.Stop();
                service.RecordMethodExecution(methodName, stopwatch.Elapsed);
            }
        }
    }
}