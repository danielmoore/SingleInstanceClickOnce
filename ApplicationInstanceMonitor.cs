using System;
using System.ServiceModel;
using Mutex = System.Threading.Mutex;

namespace NorthHorizon.Samples.SingleInstanceClickOnce
{
	[ServiceContract]
	public interface IApplicationInstanceMonitor<T>
	{
		[OperationContract(IsOneWay = true)]
		void NotifyNewInstance(T message);
	}

	[ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Single)]
	public sealed class ApplicationInstanceMonitor<T> : IApplicationInstanceMonitor<T>, IDisposable
	{
		#region Events

		public event EventHandler<NewInstanceCreatedEventArgs<T>> NewInstanceCreated;

		#endregion

		#region Fields

		private readonly string _mutexName;
		private Mutex _processLock;

		private readonly Uri _ipcUri;
		private readonly NetNamedPipeBinding _binding;
		private ServiceHost _ipcServer;
		private ChannelFactory<IApplicationInstanceMonitor<T>> _channelFactory;
		private IApplicationInstanceMonitor<T> _ipcChannel;

		#endregion

		#region Constructors

		public ApplicationInstanceMonitor() :
			this(typeof(ApplicationInstanceMonitor<>).Assembly.FullName) { }

		public ApplicationInstanceMonitor(string mutexName) : this(mutexName, mutexName) { }

		public ApplicationInstanceMonitor(string mutexName, string ipcUriPath)
		{
			_mutexName = mutexName;

			UriBuilder builder = new UriBuilder();
			builder.Scheme = Uri.UriSchemeNetPipe;
			builder.Host = "localhost";
			builder.Path = ipcUriPath;

			_ipcUri = builder.Uri;

			_binding = new NetNamedPipeBinding(NetNamedPipeSecurityMode.Transport);
		}

		#endregion

		#region Disposal

		public void Dispose()
		{
			if (_processLock != null)
				_processLock.Close();

			if (_ipcServer != null)
				_ipcServer.Close();

			if (_channelFactory != null)
				_channelFactory.Close();

			GC.SuppressFinalize(this);
		}

		#endregion

		#region Methods

		public bool Assert()
		{
			if (_processLock != null)
				throw new InvalidOperationException("Assert() has already been called.");

			bool created;
			_processLock = new Mutex(true, _mutexName, out created);

			if (created)
				StartIpcServer();
			else
				ConnectToIpcServer();

			return created;
		}

		private void StartIpcServer()
		{
			_ipcServer = new ServiceHost(this, _ipcUri);
			_ipcServer.AddServiceEndpoint(typeof(IApplicationInstanceMonitor<T>), _binding, _ipcUri);

			_ipcServer.Open();

			_ipcChannel = this;
		}

		private void ConnectToIpcServer()
		{
			_channelFactory = new ChannelFactory<IApplicationInstanceMonitor<T>>(_binding, new EndpointAddress(_ipcUri));
			_ipcChannel = _channelFactory.CreateChannel();
		}

		public void NotifyNewInstance(T message)
		{
			// Client side

			if (_ipcChannel == null)
				throw new InvalidOperationException("Not connected to IPC Server.");

			_ipcChannel.NotifyNewInstance(message);
		}

		void IApplicationInstanceMonitor<T>.NotifyNewInstance(T message)
		{
			// Server side

			if (NewInstanceCreated != null)
				NewInstanceCreated(this, new NewInstanceCreatedEventArgs<T>(message));
		}

		#endregion
	}

	public class NewInstanceCreatedEventArgs<T> : EventArgs
	{
		public NewInstanceCreatedEventArgs(T message)
			: base()
		{
			Message = message;
		}

		public T Message { get; private set; }
	}
}
