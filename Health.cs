using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.ServiceProcess;

namespace RiemannHealth {
	public interface IHealthReporter {
		bool TryGetValue(out string description, out float value);
		string Name { get; }
		float WarnThreshold { get; }
		float CriticalThreshold { get; }
	}

	public class Health {
		public static IEnumerable<IHealthReporter> Reporters(bool includeGCStats, ServiceElementCollection services) {
			yield return new Cpu();
			yield return new Load();
			yield return new Memory();
            foreach (ServiceElement service in services)
            {
                yield return new Service(service.Name);
            }
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
			if (includeGCStats) {
				yield return new DotNetGCTime();
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

		private class DotNetGCTime : IHealthReporter {
			private readonly PerformanceCounter _gcTimeCounter = new PerformanceCounter(
				".NET CLR Memory",
				"% Time in GC",
				"_Global_");
			
			private readonly PerformanceCounter _gen0HeapSizeCounter = new PerformanceCounter(
				".NET CLR Memory",
				"Gen 0 Heap Size",
				"_Global_");
			
			private readonly PerformanceCounter _gen1HeapSizeCounter = new PerformanceCounter(
				".NET CLR Memory",
				"Gen 1 Heap Size",
				"_Global_");
			
			private readonly PerformanceCounter _gen2HeapSizeCounter = new PerformanceCounter(
				".NET CLR Memory",
				"Gen 2 Heap Size",
				"_Global_");

			private readonly PerformanceCounter _gen0CollectionCounter = new PerformanceCounter(
				".NET CLR Memory",
				"# Gen 0 Collections",
				"_Global_");

			private readonly PerformanceCounter _gen1CollectionCounter = new PerformanceCounter(
				".NET CLR Memory",
				"# Gen 0 Collections",
				"_Global_");

			private readonly PerformanceCounter _gen2CollectionCounter = new PerformanceCounter(
				".NET CLR Memory",
				"# Gen 0 Collections",
				"_Global_");

			private const int mb = 1024 * 1024;

			public bool TryGetValue(out string description, out float value) {
				var gcTime = _gcTimeCounter.NextValue();
				var gen0Heap = _gen0HeapSizeCounter.NextValue();
				var gen0Collections = _gen0CollectionCounter.NextValue();
				var gen1Heap = _gen1HeapSizeCounter.NextValue();
				var gen1Collections = _gen1CollectionCounter.NextValue();
				var gen2Heap = _gen2HeapSizeCounter.NextValue();
				var gen2Collections = _gen2CollectionCounter.NextValue();
				description = string.Format(
					@"GC Time: {0:0.00} %
Gen 0 heap: {1:0.###} mb
Gen 0 collections: {2}
Gen 1 heap: {3:0.###} mb
Gen 1 collections: {4}
Gen 2 heap: {5:0.###} mb
Gen 2 collections: {6}", gcTime,
					gen0Heap / mb, gen0Collections,
					gen1Heap / mb, gen1Collections,
					gen2Heap / mb, gen2Collections);
				value = gcTime / 100;
				return true;
			}

			public string Name {
				get { return ".NET GC Time"; }
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

        public class Service : IHealthReporter
        {
            private readonly string _service;
            private ServiceController _sc;

			public Service(string service) {
                Console.WriteLine(string.Format("starting up service check for: {0}", service));
				_service = service;
                _sc = new ServiceController(service);
			}

         
            public bool TryGetValue(out string description, out float value)
            {
                string status;

                _sc.Refresh();

                try {
                    switch (_sc.Status)
                    {
                        case ServiceControllerStatus.Running:
                            status = "Running";
                            value = 1.0F;
                            break;
                        case ServiceControllerStatus.Stopped:
                            status = "Stopped";
                            value = 0.0F;
                            break;
                        case ServiceControllerStatus.Paused:
                            status = "Paused";
                            value = 0.5F;
                            break;
                        case ServiceControllerStatus.StopPending:
                            status = "Stopping";
                            value = 0.5F;
                            break;
                        case ServiceControllerStatus.StartPending:
                            status = "Starting";
                            value = 0.5F;
                            break;
                        default:
                            value = 0.0F;
                            status = "Status Changing";
                            break;
                    }

                }
                catch (InvalidOperationException e)
                {
                    value = 0.0F;
                    status = "No such service";
                }

                description = string.Format("Service {0} has status: {1}", _service, status);
                return true;
            }

            public string Name
            {
                get { return string.Format("service {0}", _service); }
            }

            public float WarnThreshold
            {
                get { return 0.5F; }
            }

            public float CriticalThreshold
            {
                get { return 0.0F; }
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