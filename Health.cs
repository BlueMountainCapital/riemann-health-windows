﻿using System;
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
			yield return new Memory();
			foreach (var drive in DriveInfo.GetDrives().Where(drive => drive.DriveType == DriveType.Fixed)) {
				yield return new Disk(drive.Name);
			}
			foreach (var network in NetworkInterface.GetAllNetworkInterfaces().Where(network => network.NetworkInterfaceType != NetworkInterfaceType.Loopback)) {
				yield return new NetworkSent(network);
				yield return new NetworkReceived(network);
			}
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
			private NetworkInterface _ni;
			private long _lastBytes;

			public NetworkReceived(NetworkInterface network) {
				_ni = network;
				_lastBytes = _ni.GetIPStatistics().BytesReceived;
			}

			public bool TryGetValue(out string description, out float value) {
				var bytes = _ni.GetIPStatistics().BytesReceived;
				value = bytes - _lastBytes;
				_lastBytes = bytes;
				description = string.Format(@"Bytes sent: {0}", _lastBytes);
				return true;
			}

			public string Name {
				get { return string.Format("{0} rx bytes", _ni.Name); }
			}

			public float WarnThreshold {
				get { return float.PositiveInfinity; }
			}

			public float CriticalThreshold {
				get { return float.PositiveInfinity; }
			}
		}
		private class NetworkSent : IHealthReporter {
			private NetworkInterface _ni;
			private long _lastBytes;

			public NetworkSent(NetworkInterface network) {
				_ni = network;
				_lastBytes = _ni.GetIPStatistics().BytesSent;
			}

			public bool TryGetValue(out string description, out float value) {
				var bytes = _ni.GetIPStatistics().BytesSent;
				value = bytes - _lastBytes;
				_lastBytes = bytes;
				description = string.Format(@"Bytes sent: {0}", _lastBytes);
				return true;
			}

			public string Name {
				get { return string.Format("{0} tx bytes", _ni.Name); }
			}

			public float WarnThreshold {
				get { return float.PositiveInfinity; }
			}

			public float CriticalThreshold {
				get { return float.PositiveInfinity; }
			}
		}
		}
}