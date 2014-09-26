#region usings
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.Streams;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;

using VVVV.Core.Logging;

using System.Net;
using System.Net.NetworkInformation;
using System.Linq;

using System.Threading;
using System.ComponentModel;

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;


#endregion usings

namespace VVVV.Nodes
{
	#region PluginInfo
	[PluginInfo(Name = "InternetAccess", Category = "Network", Version = "Value", Help = "Checks if any internet connection is available", Tags = "")]
	#endregion PluginInfo
    public class InternetAccessNode : IPluginEvaluate, IPartImportsSatisfiedNotification, IDisposable
	{
		#region fields & pins
		[Input("Interval", IsSingle = true, MinValue = 1, DefaultValue = 1)]
		IDiffSpread<int> IInterval;
		
		[Output("Internet Connection")]
		public ISpread<int> InternetConnections;
		[Output("Last Time Checked")]
		public ISpread<string> CheckenOn;
		
		[Import()]
		public ILogger Logger;
		#endregion fields & pins
		
		
		#region pin management
		public void OnImportsSatisfied()
		{
			IInterval.Changed += delegate(IDiffSpread<int> Intervals) {
				lock (IntervalLock) {
					Interval = Intervals[0];
				}
			};
			StartConnectionMonitor();
		}
		public InternetAccessNode():base() {
			ID++;
		}
		// Do it in a BackgroundWorker and shrare it among the nodes....
		static BackgroundWorker Worker = new BackgroundWorker();
		static int ID = 0;
		static Mutex IntervalLock = new Mutex();
		static int Interval = 1;
		void StartConnectionMonitor() {
			InternetConnections.SliceCount = 1;
			CheckenOn.SliceCount = 1;
			Worker.RunWorkerCompleted += delegate(object sender, RunWorkerCompletedEventArgs args) {
				if (args.Cancelled) Worker.Dispose();
			};
			Worker.DoWork += delegate (object sender, DoWorkEventArgs args) {
				try {
					BackgroundWorker w = sender as BackgroundWorker;
					while(!w.CancellationPending) {
						w.ReportProgress(IsAvailableNetworkActive() ? 1 : 0);
						
						int _Interval = 1;
						lock(IntervalLock) {
							_Interval = Interval;
						}
						for	(int i=0; i < 100 * Interval; i++) {
							Thread.Sleep(10);
							args.Cancel = w.CancellationPending;
							if(args.Cancel) return;
						}
					}
					
				} catch( Exception e) {}
			};
			
			Worker.ProgressChanged += delegate(object sender, ProgressChangedEventArgs args) {
				try {
					InternetConnections[0] = args.ProgressPercentage;
					CheckenOn[0] = DateTime.Now.ToLocalTime().ToString();
				} catch (Exception e) {}
			};
			
			if (Worker.IsBusy) return;
			Worker.WorkerReportsProgress = true;
			Worker.WorkerSupportsCancellation = true;
			Worker.RunWorkerAsync();
		}
		
		#endregion
		
		public void Dispose() {
			ID--;
			if (ID < 1) Worker.CancelAsync();
		}
		
		// Called when data for any output pin is requested.
		public void Evaluate(int SpreadMax)
		{
		}
		
		
		// AS OF: http://stackoverflow.com/a/24473982
		public static bool IsAvailableNetworkActive()
		{
			// only recognizes changes related to Internet adapters
			if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
			{
				// however, this will include all adapters -- filter by opstatus and activity
				NetworkInterface[] interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
				return (from face in interfaces
				where face.OperationalStatus == OperationalStatus.Up
				where (face.NetworkInterfaceType != NetworkInterfaceType.Tunnel) && (face.NetworkInterfaceType != NetworkInterfaceType.Loopback)
				select face.GetIPv4Statistics()).Any(statistics => (statistics.BytesReceived > 0) && (statistics.BytesSent > 0));
			}
			
			return false;
		}
	}
}
