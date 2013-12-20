using System;
using System.Configuration;
using System.Linq;
using System.Threading;
using Riemann;

namespace RiemannHealth {
	public class Program {
		public static void Main(string[] args) {
			string hostname;
			ushort port;
			double interval = 1.0;
			ushort ttl = 5;
			bool includeGCStats;
			switch (args.Length) {
				case 0:
					var appSettings = ConfigurationManager.AppSettings;
					hostname = appSettings["RiemannHost"];
					port = UInt16.Parse(appSettings["RiemannPort"]);
					interval = (float)UInt16.Parse(appSettings["Interval"]);
					ttl = UInt16.Parse(appSettings["TTL"]);
					includeGCStats = Boolean.Parse(appSettings["IncludeGCstats"]);
					break;
				case 1:
					hostname = args[0];
					port = 5555;
					includeGCStats = true;
					break;
				case 2:
					hostname = args[0];
					if (!ushort.TryParse(args[1], out port)) {
						Usage();
						Environment.Exit(-1);
					}
					includeGCStats = true;
					break;
				default:
					Usage();
					Environment.Exit(-1);
					return;
			}
			var client = new Client(hostname, port);

			var reporters = Health.Reporters(includeGCStats)
				.ToList();
			while (true) {
				foreach (var reporter in reporters) {
					string description;
					float value;

					if (reporter.TryGetValue(out description, out value)) {
						string state;
						if (value >= reporter.CriticalThreshold) {
							state = "critical";
						} else if (value >= reporter.WarnThreshold) {
							state = "warning";
						} else {
							state = "ok";
						}
						client.SendEvent(reporter.Name, state, description, value, ttl);
					}
				}
				Thread.Sleep(TimeSpan.FromSeconds(interval));
			}
		}


		private static void Usage() {
			Console.WriteLine(@"riemann [[riemann-host] [riemann-port]]
If not including the host and port, please modify the App.config to suit your needs.");
		}
	}
}
