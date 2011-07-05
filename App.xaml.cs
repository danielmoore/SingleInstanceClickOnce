using System;
using System.Windows;
using ApplicationDeployment = System.Deployment.Application.ApplicationDeployment;
using HttpUtility = System.Web.HttpUtility;

namespace NorthHorizon.Samples.SingleInstanceClickOnce
{
	public partial class App : Application
	{
		private Window1 _window;
		private ApplicationInstanceMonitor<MyMessage> _instanceMonitor;

		public App()
		{
			_window = new Window1();
			_instanceMonitor = new ApplicationInstanceMonitor<MyMessage>();
		}

		protected override void OnStartup(StartupEventArgs e)
		{
			if (_instanceMonitor.Assert())
			{
				// This is the only instance.

				_instanceMonitor.NewInstanceCreated += OnNewInstanceCreated;

				HandleQueryString(GetQueryString());
				_window.Show();
			}
			else
			{
				// Defer to another instance.

				_instanceMonitor.NotifyNewInstance(new MyMessage { QueryString = GetQueryString() });

				Shutdown();
			}
		}

		private void OnNewInstanceCreated(object sender, NewInstanceCreatedEventArgs<MyMessage> e)
		{
			HandleQueryString(e.Message.QueryString);

			_window.Activate();
		}

		public string GetQueryString()
		{
			return ApplicationDeployment.IsNetworkDeployed ?
				ApplicationDeployment.CurrentDeployment.ActivationUri.Query :
				string.Empty;
		}

		public void HandleQueryString(string query)
		{
			var args = HttpUtility.ParseQueryString(query);

			_window.Message = args["message"] ?? "No message provided";
		}
	}

	[Serializable]
	public class MyMessage
	{
		public string QueryString { get; set; }
	}
}
