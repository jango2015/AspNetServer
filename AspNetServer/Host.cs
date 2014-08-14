namespace AspNetServer
{
    using System;
    using System.Globalization;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Security.Permissions;
    using System.Security.Principal;
    using System.Threading;
    using System.Web;
    using System.Web.Hosting;

    internal sealed class Host : MarshalByRefObject, IRegisteredObject
    {
        private bool _disableDirectoryListing;
        private string _installPath;
        private string _lowerCasedClientScriptPathWithTrailingSlash;
        private string _lowerCasedVirtualPath;
        private string _lowerCasedVirtualPathWithTrailingSlash;
        private volatile int _pendingCallsCount;
        private string _physicalClientScriptPath;
        private string _physicalPath;
        private int _port;
        private bool _requireAuthentication;
        private Server _server;
        private string _virtualPath;

        public Host()
        {
            HostingEnvironment.RegisterObject(this);
        }

        private void AddPendingCall()
        {
            Interlocked.Increment(ref this._pendingCallsCount);
        }

        public void Configure(Server server, int port, string virtualPath, string physicalPath, bool requireAuthentication)
        {
            this.Configure(server, port, virtualPath, physicalPath, requireAuthentication, false);
        }

        public void Configure(Server server, int port, string virtualPath, string physicalPath, bool requireAuthentication, bool disableDirectoryListing)
        {
            this._server = server;
            this._port = port;
            this._installPath = null;
            this._virtualPath = virtualPath;
            this._requireAuthentication = requireAuthentication;
            this._disableDirectoryListing = disableDirectoryListing;
            this._lowerCasedVirtualPath = CultureInfo.InvariantCulture.TextInfo.ToLower(this._virtualPath);
            this._lowerCasedVirtualPathWithTrailingSlash = virtualPath.EndsWith("/", StringComparison.Ordinal) ? virtualPath : (virtualPath + "/");
            this._lowerCasedVirtualPathWithTrailingSlash = CultureInfo.InvariantCulture.TextInfo.ToLower(this._lowerCasedVirtualPathWithTrailingSlash);
            this._physicalPath = physicalPath;
            this._physicalClientScriptPath = HttpRuntime.AspClientScriptPhysicalPath + @"\";
            this._lowerCasedClientScriptPathWithTrailingSlash = CultureInfo.InvariantCulture.TextInfo.ToLower(HttpRuntime.AspClientScriptVirtualPath + "/");
        }

        public SecurityIdentifier GetProcessSID()
        {
            using (WindowsIdentity identity = new WindowsIdentity(this._server.GetProcessToken()))
            {
                return identity.User;
            }
        }

        public IntPtr GetProcessToken()
        {
            new SecurityPermission(PermissionState.Unrestricted).Assert();
            return this._server.GetProcessToken();
        }

        public string GetProcessUser()
        {
            return this._server.GetProcessUser();
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }

        public bool IsVirtualPathAppPath(string path)
        {
            if (path == null)
            {
                return false;
            }
            path = CultureInfo.InvariantCulture.TextInfo.ToLower(path);
            if (!(path == this._lowerCasedVirtualPath))
            {
                return (path == this._lowerCasedVirtualPathWithTrailingSlash);
            }
            return true;
        }

        public bool IsVirtualPathInApp(string path)
        {
            bool flag;
            return this.IsVirtualPathInApp(path, out flag);
        }

        public bool IsVirtualPathInApp(string path, out bool isClientScriptPath)
        {
            isClientScriptPath = false;
            if (path != null)
            {
                path = CultureInfo.InvariantCulture.TextInfo.ToLower(path);
                if ((this._virtualPath == "/") && path.StartsWith("/", StringComparison.Ordinal))
                {
                    if (path.StartsWith(this._lowerCasedClientScriptPathWithTrailingSlash, StringComparison.Ordinal))
                    {
                        isClientScriptPath = true;
                    }
                    return true;
                }
                if (path.StartsWith(this._lowerCasedVirtualPathWithTrailingSlash, StringComparison.Ordinal))
                {
                    return true;
                }
                if (path == this._lowerCasedVirtualPath)
                {
                    return true;
                }
                if (path.StartsWith(this._lowerCasedClientScriptPathWithTrailingSlash, StringComparison.Ordinal))
                {
                    isClientScriptPath = true;
                    return true;
                }
            }
            return false;
        }

        public void ProcessRequest(Connection conn)
        {
            this.AddPendingCall();
            try
            {
                new Request(this, conn).Process();
            }
            finally
            {
                this.RemovePendingCall();
            }
        }

        private void RemovePendingCall()
        {
            Interlocked.Decrement(ref this._pendingCallsCount);
        }

        [SecurityPermission(SecurityAction.Assert, Unrestricted=true)]
        public void Shutdown()
        {
            HostingEnvironment.InitiateShutdown();
        }

        void IRegisteredObject.Stop(bool immediate)
        {
            if (this._server != null)
            {
                this._server.HostStopped();
            }
            this.WaitForPendingCallsToFinish();
            HostingEnvironment.UnregisterObject(this);
        }

        private void WaitForPendingCallsToFinish()
        {
            while (this._pendingCallsCount > 0)
            {
                Thread.Sleep(250);
            }
        }

        public bool DisableDirectoryListing
        {
            get
            {
                return this._disableDirectoryListing;
            }
        }

        public string InstallPath
        {
            get
            {
                return this._installPath;
            }
        }

        public string NormalizedClientScriptPath
        {
            get
            {
                return this._lowerCasedClientScriptPathWithTrailingSlash;
            }
        }

        public string NormalizedVirtualPath
        {
            get
            {
                return this._lowerCasedVirtualPathWithTrailingSlash;
            }
        }

        public string PhysicalClientScriptPath
        {
            get
            {
                return this._physicalClientScriptPath;
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

        public bool RequireAuthentication
        {
            get
            {
                return this._requireAuthentication;
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

