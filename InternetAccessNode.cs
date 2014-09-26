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

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

using System.Threading;

using NETWORKLIST;

#endregion usings

namespace VVVV.Nodes
{
	#region PluginInfo
	[PluginInfo(Name = "InternetAccess", Category = "Network", Version = "Value", Author = "jens", Help = "Checks if any internet connection is available", Tags = "")]
	#endregion PluginInfo
    public class InternetAccessNode : IPluginEvaluate, IPartImportsSatisfiedNotification, IDisposable, INetworkListManagerEvents
	{
		#region fields & pins
		[Output("Internet Connection", IsToggle=true)]
		public ISpread<bool> InternetConnections;

        [Output("Available Networks")]
        public ISpread<string> AvailableNetworks;

		[Output("Last Time Networks Changed")]
		public ISpread<string> CheckenOn;
		
		[Import()]
		public ILogger Logger;
		#endregion fields & pins

        private int m_cookie = 0;
        private IConnectionPoint m_icp;
        private INetworkListManager m_nlm;
        private bool HasInternet = false;
        private DateTime LastCheck = DateTime.MinValue;

		#region pin management
		public void OnImportsSatisfied()
		{
            AvailableNetworks.SliceCount = 0;
            Networks = NLM.GetNetworks(NLM_ENUM_NETWORK.NLM_ENUM_NETWORK_CONNECTED);
            LastCheck = DateTime.Now;
            HasInternet = NLM.IsConnectedToInternet;

            AdviseforNetworklistManager();
            InternetConnections.SliceCount = 1;
            CheckenOn.SliceCount = 1;
		}

		public InternetAccessNode():base() {
            m_nlm = new NetworkListManager();
            Networks = NLM.GetNetworks(NLM_ENUM_NETWORK.NLM_ENUM_NETWORK_CONNECTED);
		}

        IEnumNetworks Networks;

        public INetworkListManager NLM
        {
            get
            {
                return m_nlm;
            }
        }


        public void ConnectivityChanged(NLM_CONNECTIVITY newConnectivity)
        {
            //Console.WriteLine("");
            //Console.WriteLine("---------------------------------------------------------------------------");
            //Console.WriteLine("NetworkList just informed the connectivity change. The new connectivity is:");
            if (newConnectivity == NLM_CONNECTIVITY.NLM_CONNECTIVITY_DISCONNECTED)
            {
               // Console.WriteLine("The machine is disconnected from Network");
                HasInternet = false;
            }
            if (((int)newConnectivity & (int)NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV4_INTERNET) != 0)
            {
                //Console.WriteLine("The machine is connected to internet with IPv4 capability ");
                HasInternet = true;
            }
            if (((int)newConnectivity & (int)NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV6_INTERNET) != 0)
            {
                //Console.WriteLine("The machine is connected to internet with IPv6 capability ");
                HasInternet = true;
            }
            if ((((int)newConnectivity & (int)NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV4_INTERNET) == 0)
                && (((int)newConnectivity & (int)NLM_CONNECTIVITY.NLM_CONNECTIVITY_IPV6_INTERNET) == 0))
            {
                //Console.WriteLine("The machine is not connected to internet yet ");
                HasInternet = false;
            }
            Networks = NLM.GetNetworks(NLM_ENUM_NETWORK.NLM_ENUM_NETWORK_CONNECTED);
            LastCheck = DateTime.Now;
        }
        public void AdviseforNetworklistManager()
        {
            IConnectionPointContainer icpc = (IConnectionPointContainer)m_nlm;
            Guid tempGuid = typeof(INetworkListManagerEvents).GUID;
            icpc.FindConnectionPoint(ref tempGuid, out m_icp);
            m_icp.Advise(this, out m_cookie);
        }

        public void UnAdviseforNetworklistManager()
        {
            m_icp.Unadvise(m_cookie);
        }        

		#endregion
		
		public void Dispose() {
            UnAdviseforNetworklistManager();
		}
		
		// Called when data for any output pin is requested.
		public void Evaluate(int SpreadMax)
		{
            InternetConnections[0] = HasInternet;
            CheckenOn[0] = LastCheck.ToString();
            try
            {
                AvailableNetworks.AssignFrom(from INetwork net in Networks where net.IsConnectedToInternet select net.GetName());
            }
            catch (Exception e) { }
		}
		
	}
}
