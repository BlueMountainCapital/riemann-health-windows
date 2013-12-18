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
			bool includeGCStats;
			switch (args.Length) {
				case 0:
					var appSettings = ConfigurationManager.AppSettings;
					hostname = appSettings["RiemannHost"];
					port = UInt16.Parse(appSettings["RiemannPort"]);
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
						} else if (value >= reporter.WarningThreshold) {
							state = "warning";
						} else {
							state = "ok";
						}
						client.SendEvent(reporter.Name, state, description, value, 1);
					}
				}
				Thread.Sleep(TimeSpan.FromSeconds(1.0));
			}
		}


		private static void Usage() {
			Console.WriteLine(@"riemann [[riemann-host] [riemann-port]]
If not including the host and port, please modify the App.config to suit your needs.");
		}
	}
}
