using System;
using System.Linq;
using System.Threading;
using Riemann;

namespace RiemannHealth {
	public class Program {
		public static void Main(string[] args) {
			string hostname;
			ushort port;
			switch (args.Length) {
				case 0:
					hostname = "localhost";
					port = 5555;
					break;
				case 1:
					hostname = args[0];
					port = 5555;
					break;
				case 2:
					hostname = args[0];
					if (!ushort.TryParse(args[1], out port)) {
						Usage();
						Environment.Exit(-1);
					}
					break;
				default:
					Usage();
					Environment.Exit(-1);
					return;
			}
			var client = new Client(hostname, port);

			var reporters = Health.Reporters()
				.ToList();
			while (true) {
				foreach (var reporter in reporters) {
					string description;
					float value;

					if (reporter.TryGetValue(out description, out value)) {
						string state;
						if (value < reporter.WarnThreshold) {
							state = "ok";
						} else if (value < reporter.CriticalThreshold) {
							state = "warning";
						} else {
							state = "critical";
						}
						client.SendEvent(reporter.Name, state, description, value, 1);
					}
				}
				Thread.Sleep(TimeSpan.FromSeconds(1.0));
			}
		}


		private static void Usage() {
			Console.WriteLine(@"riemann [riemann-host] [riemann-port]");
		}
	}
}
