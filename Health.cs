using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace RiemannHealth {
	public interface IHealthReporter {
		bool TryGetValue(out string description, out float value);
		string Name { get; }
		float WarnThreshold { get; }
		float CriticalThreshold { get; }
	}

	public class Health {
		public static IEnumerable<IHealthReporter> Reporters() {
			yield return new Cpu();
			yield return new Memory();
		}

		private class Cpu : IHealthReporter {
			private readonly PerformanceCounter _counter = new PerformanceCounter(
				"Processor",
				"% Processor Time",
				"_Total");

			private readonly int _cpuCount = Environment.ProcessorCount;

			public bool TryGetValue(out string description, out float value) {
				var cpu = _counter.NextValue() / _cpuCount;
				description = string.Format("CPU Load: {0} %", cpu);
				value = cpu / 100.0f;
				return true;
			}

			public string Name {
				get { return "cpu"; }
			}

			public float WarnThreshold {
				get { return 0.90f; }
			}

			public float CriticalThreshold {
				get { return 0.95f; }
			}
		}

		private class Memory : IHealthReporter {
			[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
			private class MEMORYSTATUSEX {
				public uint dwLength;
				public uint dwMemoryLoad;
				public ulong ullTotalPhys;
				public ulong ullAvailPhys;
				public ulong ullTotalPageFile;
				public ulong ullAvailPageFile;
				public ulong ullTotalVirtual;
				public ulong ullAvailVirtual;
				public ulong ullAvailExtendedVirtual;
				public MEMORYSTATUSEX() {
					this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
				}
			}

			[return: MarshalAs(UnmanagedType.Bool)]
			[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
			static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

			public bool TryGetValue(out string description, out float value) {
				var memoryStatusEx = new MEMORYSTATUSEX();
				if (!GlobalMemoryStatusEx(memoryStatusEx)) {
					description = "";
					value = 10000.0f;
					return false;
				}
				value = memoryStatusEx.dwMemoryLoad / 100.0f;
				description = string.Format(
					@"Available Physical Memory: {0}
Total Physical Memory: {1}
Memory Load: {2}
Available Page File: {3}
Total Page File: {4}",
					memoryStatusEx.ullAvailPhys,
					memoryStatusEx.ullTotalPhys,
					memoryStatusEx.dwMemoryLoad,
					memoryStatusEx.ullAvailPageFile,
					memoryStatusEx.ullTotalPageFile);
				return true;
			}

			public string Name {
				get { return "memory"; }
			}

			public float WarnThreshold {
				get { return 0.90f; }
			}

			public float CriticalThreshold {
				get { return 0.95f; }
			}
		}
	}
}