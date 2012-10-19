using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

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
			yield return new Load();
			yield return new Memory();
			foreach (var drive in DriveInfo.GetDrives().Where(drive => drive.DriveType == DriveType.Fixed)) {
				yield return new Disk(drive.Name);
			}
			var interfaces = NetworkInterface.GetAllNetworkInterfaces()
				.GroupBy(network => network.NetworkInterfaceType)
				.Where(network => network.Key != NetworkInterfaceType.Loopback);
			foreach (var networkType in interfaces) {
				var nt = networkType.ToList();
				for (var i = 0; i < nt.Count; i++) {
					var name = Translate(networkType.Key) + i;
					yield return new NetworkSent(nt[i], name);
					yield return new NetworkReceived(nt[i], name);
				}
			}
		}

		private static string Translate(NetworkInterfaceType key) {
			switch (key) {
				case NetworkInterfaceType.Ethernet:
					return "eth";
				default:
					return key.ToString();
			}
		}

		private class Cpu : IHealthReporter {
			private readonly PerformanceCounter _counter = new PerformanceCounter(
				"Processor",
				"% Processor Time",
				"_Total");

			public bool TryGetValue(out string description, out float value) {
				var cpu = _counter.NextValue();
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
		
		private class Load : IHealthReporter {
			private readonly PerformanceCounter _counter = new PerformanceCounter(
				"System",
				"Processor Queue Length");

			private readonly int _cpuCount = Environment.ProcessorCount;

			public bool TryGetValue(out string description, out float value) {
				var load = _counter.NextValue();
				description = string.Format("Processor queue length: {0}", load);
				value = load;
				return true;
			}

			public string Name {
				get { return "load"; }
			}

			public float WarnThreshold {
				get { return _cpuCount * 2.0f; }
			}

			public float CriticalThreshold {
				get { return _cpuCount * 2.5f; }
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
				value = 1.0f - (((float) memoryStatusEx.ullAvailPhys / memoryStatusEx.ullTotalPhys));
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

		private class Disk : IHealthReporter {
			private readonly string _drive;
			public Disk(string drive) {
				_drive = drive;
			}

			public bool TryGetValue(out string description, out float value) {
				var drive = new DriveInfo(_drive);
				if (!drive.IsReady) {
					description = null;
					value = 0;
					return false;
				}
				value = 1.0f - (((float) drive.AvailableFreeSpace) / drive.TotalSize);
				description = string.Format(
					@"Available Space: {0}
Total Free Space: {1}
Total Size: {2}", drive.AvailableFreeSpace, drive.TotalFreeSpace, drive.TotalSize);
				return true;
			}

			public string Name { get { return "disk " + _drive; } }
			public float WarnThreshold { get { return 0.90f; } }
			public float CriticalThreshold { get { return 0.95f; } }
		}

		private class NetworkReceived : IHealthReporter {
			private readonly NetworkInterface _ni;
			private long _lastBytes;

			public NetworkReceived(NetworkInterface network, string name) {
				_ni = network;
				Name = string.Format("{0} rx bytes", name);
				_lastBytes = _ni.GetIPStatistics().BytesReceived;
			}

			public bool TryGetValue(out string description, out float value) {
				if (_ni.OperationalStatus == OperationalStatus.Up) {
					var bytes = _ni.GetIPStatistics().BytesReceived;
					value = bytes - _lastBytes;
					_lastBytes = bytes;
					description = string.Format(@"Bytes received: {0}", value);
					return true;
				}
				value = float.NaN;
				description = null;
				return false;
			}

			public string Name { get; private set; }

			public float WarnThreshold {
				get { return float.PositiveInfinity; }
			}

			public float CriticalThreshold {
				get { return float.PositiveInfinity; }
			}
		}
		private class NetworkSent : IHealthReporter {
			private readonly NetworkInterface _ni;
			private long _lastBytes;

			public NetworkSent(NetworkInterface network, string name) {
				_ni = network;
				Name = string.Format("{0} tx bytes", name);
				_lastBytes = _ni.GetIPStatistics().BytesSent;
			}

			public bool TryGetValue(out string description, out float value) {
				if (_ni.OperationalStatus == OperationalStatus.Up) {
					var bytes = _ni.GetIPStatistics().BytesSent;
					value = bytes - _lastBytes;
					_lastBytes = bytes;
					description = string.Format(@"Bytes sent: {0}", value);
					return true;
				}
				value = float.NaN;
				description = null;
				return false;
			}

			public string Name { get; private set; }

			public float WarnThreshold {
				get { return float.PositiveInfinity; }
			}

			public float CriticalThreshold {
				get { return float.PositiveInfinity; }
			}
		}
		}
}