namespace AspNetServer
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Security.Permissions;
    using System.Security.Principal;
    using System.Threading;
    using System.Web.Hosting;

    [PermissionSet(SecurityAction.InheritanceDemand, Name="FullTrust"), PermissionSet(SecurityAction.LinkDemand, Name="Everything")]
    public class Server : MarshalByRefObject
    {
        private ApplicationManager _appManager;
        private bool _disableDirectoryListing;
        private Host _host;
        private object _lockObject;
        private WaitCallback _onSocketAccept;
        private WaitCallback _onStart;
        private string _physicalPath;
        private int _port;
        private IntPtr _processToken;
        private string _processUser;
        private bool _requireAuthentication;
        private bool _shutdownInProgress;
        private Socket _socket;
        private string _virtualPath;
        private const int SecurityImpersonation = 2;
        private const int TOKEN_ALL_ACCESS = 0xf01ff;
        private const int TOKEN_EXECUTE = 0x20000;
        private const int TOKEN_IMPERSONATE = 4;
        private const int TOKEN_READ = 0x20008;

        public Server(int port, string virtualPath, string physicalPath) : this(port, virtualPath, physicalPath, false, false)
        {
        }

        public Server(int port, string virtualPath, string physicalPath, bool requireAuthentication) : this(port, virtualPath, physicalPath, requireAuthentication, false)
        {
        }

        public Server(int port, string virtualPath, string physicalPath, bool requireAuthentication, bool disableDirectoryListing)
        {
            this._lockObject = new object();
            this._port = port;
            this._virtualPath = virtualPath;
            this._physicalPath = physicalPath.EndsWith(@"\", StringComparison.Ordinal) ? physicalPath : (physicalPath + @"\");
            this._requireAuthentication = requireAuthentication;
            this._disableDirectoryListing = disableDirectoryListing;
            this._onSocketAccept = new WaitCallback(this.OnSocketAccept);
            this._onStart = new WaitCallback(this.OnStart);
            this._appManager = ApplicationManager.GetApplicationManager();
            this.ObtainProcessToken();
        }

        private Socket CreateSocketBindAndListen(AddressFamily family, IPAddress address, int port)
        {
            Socket socket = new Socket(family, SocketType.Stream, ProtocolType.Tcp) {
                ExclusiveAddressUse = true
            };
            try
            {
                socket.Bind(new IPEndPoint(address, port));
            }
            catch
            {
                socket.ExclusiveAddressUse = false;
                try
                {
                    socket.Bind(new IPEndPoint(address, port));
                }
                catch
                {
                    socket.Close();
                    socket = null;
                    throw;
                }
            }
            if (socket != null)
            {
                socket.Listen(0x7fffffff);
            }
            return socket;
        }

        [DllImport("KERNEL32.DLL", SetLastError=true)]
        private static extern IntPtr GetCurrentThread();
        private Host GetHost()
        {
            if (this._shutdownInProgress)
            {
                return null;
            }
            Host host = this._host;
            if (host == null)
            {
                lock (this._lockObject)
                {
                    this.InitHost();
                    string appId = (this._virtualPath + this._physicalPath).ToLowerInvariant().GetHashCode().ToString("x", CultureInfo.InvariantCulture);
                    this._host = (Host) this._appManager.CreateObject(appId, typeof(Host), this._virtualPath, this._physicalPath, false);
                    this._host.Configure(this, this._port, this._virtualPath, this._physicalPath, this._requireAuthentication);
                    host = this._host;
                }
            }
            return host;
        }

        public IntPtr GetProcessToken()
        {
            return this._processToken;
        }

        public string GetProcessUser()
        {
            return this._processUser;
        }

        internal void HostStopped()
        {
            this._host = null;
        }

        [DllImport("ADVAPI32.DLL", SetLastError=true)]
        private static extern bool ImpersonateSelf(int level);
        private void InitHost()
        {
            string path = Path.Combine(this._physicalPath, "bin");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            string location = Assembly.GetExecutingAssembly().Location;
            string str3 = Path.Combine(path,Assembly.GetExecutingAssembly().GetName().Name + ".exe");
            if (System.IO.File.Exists(str3))
            {
                System.IO.File.Delete(str3);
            }
            System.IO.File.Copy(location, str3);
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }

        private void ObtainProcessToken()
        {
            if (ImpersonateSelf(2))
            {
                OpenThreadToken(GetCurrentThread(), 0xf01ff, true, ref this._processToken);
                RevertToSelf();
                this._processUser = WindowsIdentity.GetCurrent().Name;
            }
        }

        private void OnSocketAccept(object acceptedSocket)
        {
            if (!this._shutdownInProgress)
            {
                Connection conn = new Connection(this, (Socket) acceptedSocket);
                if (conn.WaitForRequestBytes() == 0)
                {
                    conn.WriteErrorAndClose(400);
                }
                else
                {
                    Host host = this.GetHost();
                    if (host == null)
                    {
                        conn.WriteErrorAndClose(500);
                    }
                    else
                    {
                        host.ProcessRequest(conn);
                    }
                }
            }
        }

        private void OnStart(object unused)
        {
            while (!this._shutdownInProgress)
            {
                try
                {
                    Socket state = this._socket.Accept();
                    ThreadPool.QueueUserWorkItem(this._onSocketAccept, state);
                    continue;
                }
                catch
                {
                    Thread.Sleep(100);
                    continue;
                }
            }
        }

        [DllImport("ADVAPI32.DLL", SetLastError=true)]
        private static extern int OpenThreadToken(IntPtr thread, int access, bool openAsSelf, ref IntPtr hToken);
        [DllImport("ADVAPI32.DLL", SetLastError=true)]
        private static extern int RevertToSelf();
        public void Start()
        {
            try
            {
                this._socket = this.CreateSocketBindAndListen(AddressFamily.InterNetwork, IPAddress.Loopback, this._port);
            }
            catch (Exception exception)
            {
                SocketException exception2 = exception as SocketException;
                if ((exception2 != null) && (exception2.SocketErrorCode == SocketError.AddressAlreadyInUse))
                {
                    throw exception;
                }
                this._socket = this.CreateSocketBindAndListen(AddressFamily.InterNetworkV6, IPAddress.IPv6Loopback, this._port);
            }
            if (this._socket != null)
            {
                Console.WriteLine("Serving HTTP on 0.0.0.0 port " + Port + " ...");
                ThreadPool.QueueUserWorkItem(this._onStart);
            }
        }

        public void Stop()
        {
            this._shutdownInProgress = true;
            try
            {
                if (this._socket != null)
                {
                    this._socket.Close();
                }
            }
            catch
            {
            }
            finally
            {
                this._socket = null;
            }
            try
            {
                if (this._host != null)
                {
                    this._host.Shutdown();
                }
                while (this._host != null)
                {
                    Thread.Sleep(100);
                }
            }
            catch
            {
            }
            finally
            {
                this._host = null;
            }
        }

        public string PhysicalPath
        {
            get
            {
                return this._physicalPath;
            }
        }

        public int Port
        {
            get
            {
                return this._port;
            }
        }

        public string RootUrl
        {
            get
            {
                if (this._port != 80)
                {
                    return ("http://localhost:" + this._port + this._virtualPath);
                }
                return ("http://localhost" + this._virtualPath);
            }
        }

        public string VirtualPath
        {
            get
            {
                return this._virtualPath;
            }
        }
    }
}

