namespace AspNetServer
{
    using Microsoft.Win32.SafeHandles;
    using System;
    using System.Collections;
    using System.Globalization;
    using System.IO;
    using System.Security;
    using System.Security.Permissions;
    using System.Text;
    using System.Web;
    using System.Web.Hosting;

    internal sealed class Request : SimpleWorkerRequest
    {
        private string _allRawHeaders;
        private Connection _connection;
        private IStackWalk _connectionPermission;
        private int _contentLength;
        private int _endHeadersOffset;
        private string _filePath;
        private byte[] _headerBytes;
        private ArrayList _headerByteStrings;
        private bool _headersSent;
        private Host _host;
        private bool _isClientScriptPath;
        private string[] _knownRequestHeaders;
        private string _path;
        private string _pathInfo;
        private string _pathTranslated;
        private byte[] _preloadedContent;
        private int _preloadedContentLength;
        private string _prot;
        private string _queryString;
        private byte[] _queryStringBytes;
        private ArrayList _responseBodyBytes;
        private StringBuilder _responseHeadersBuilder;
        private int _responseStatus;
        private bool _specialCaseStaticFileHeaders;
        private int _startHeadersOffset;
        private string[][] _unknownRequestHeaders;
        private string _url;
        private string _verb;
        private static char[] badPathChars = new char[] { '%', '>', '<', ':', '\\' };
        private static string[] defaultFileNames = new string[] { "default.aspx", "default.htm", "default.html" };
        private static char[] IntToHex = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f' };
        private const int MaxChunkLength = 0x10000;
        private const int maxHeaderBytes = 0x8000;
        private static string[] restrictedDirs = new string[] { "/bin", "/app_browsers", "/app_code", "/app_data", "/app_localresources", "/app_globalresources", "/app_webreferences" };

        public Request(Host host, Connection connection) : base(string.Empty, string.Empty, null)
        {
            this._connectionPermission = new PermissionSet(PermissionState.Unrestricted);
            this._host = host;
            this._connection = connection;
        }

        public override void CloseConnection()
        {
            this._connectionPermission.Assert();
            this._connection.Close();
        }

        public override void EndOfRequest()
        {
        }

        public override void FlushResponse(bool finalFlush)
        {
            if ((((this._responseStatus != 0x194) || this._headersSent) || (!finalFlush || (this._verb != "GET"))) || !this.ProcessDirectoryListingRequest())
            {
                this._connectionPermission.Assert();
                if (!this._headersSent)
                {
                    this._connection.WriteHeaders(this._responseStatus, this._responseHeadersBuilder.ToString());
                    this._headersSent = true;
                }
                for (int i = 0; i < this._responseBodyBytes.Count; i++)
                {
                    byte[] data = (byte[]) this._responseBodyBytes[i];
                    this._connection.WriteBody(data, 0, data.Length);
                }
                this._responseBodyBytes = new ArrayList();
                if (finalFlush)
                {
                    this._connection.Close();
                }
            }
        }

        public override string GetAppPath()
        {
            return this._host.VirtualPath;
        }

        public override string GetAppPathTranslated()
        {
            return this._host.PhysicalPath;
        }

        public override string GetFilePath()
        {
            return this._filePath;
        }

        public override string GetFilePathTranslated()
        {
            return this._pathTranslated;
        }

        public override string GetHttpVerbName()
        {
            return this._verb;
        }

        public override string GetHttpVersion()
        {
            return this._prot;
        }

        public override string GetKnownRequestHeader(int index)
        {
            return this._knownRequestHeaders[index];
        }

        public override string GetLocalAddress()
        {
            this._connectionPermission.Assert();
            return this._connection.LocalIP;
        }

        public override int GetLocalPort()
        {
            return this._host.Port;
        }

        public override string GetPathInfo()
        {
            return this._pathInfo;
        }

        public override byte[] GetPreloadedEntityBody()
        {
            return this._preloadedContent;
        }

        public override string GetQueryString()
        {
            return this._queryString;
        }

        public override byte[] GetQueryStringRawBytes()
        {
            return this._queryStringBytes;
        }

        public override string GetRawUrl()
        {
            return this._url;
        }

        public override string GetRemoteAddress()
        {
            this._connectionPermission.Assert();
            return this._connection.RemoteIP;
        }

        public override int GetRemotePort()
        {
            return 0;
        }

        public override string GetServerName()
        {
            string localAddress = this.GetLocalAddress();
            if (localAddress.Equals("127.0.0.1"))
            {
                return "localhost";
            }
            return localAddress;
        }

        public override string GetServerVariable(string name)
        {
            string processUser = string.Empty;
            string str2 = name;
            switch (str2)
            {
                case null:
                    return processUser;

                case "ALL_RAW":
                    return this._allRawHeaders;

                case "SERVER_PROTOCOL":
                    return this._prot;

                case "LOGON_USER":
                    if (this.GetUserToken() != IntPtr.Zero)
                    {
                        processUser = this._host.GetProcessUser();
                    }
                    return processUser;
            }
            if ((str2 == "AUTH_TYPE") && (this.GetUserToken() != IntPtr.Zero))
            {
                processUser = "NTLM";
            }
            return processUser;
        }

        public override string GetUnknownRequestHeader(string name)
        {
            int length = this._unknownRequestHeaders.Length;
            for (int i = 0; i < length; i++)
            {
                if (string.Compare(name, this._unknownRequestHeaders[i][0], StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return this._unknownRequestHeaders[i][1];
                }
            }
            return null;
        }

        public override string[][] GetUnknownRequestHeaders()
        {
            return this._unknownRequestHeaders;
        }

        public override string GetUriPath()
        {
            return this._path;
        }

        public override IntPtr GetUserToken()
        {
            return this._host.GetProcessToken();
        }

        public override bool HeadersSent()
        {
            return this._headersSent;
        }

        private bool IsBadPath()
        {
            return ((this._path.IndexOfAny(badPathChars) >= 0) || ((CultureInfo.InvariantCulture.CompareInfo.IndexOf(this._path, "..", CompareOptions.Ordinal) >= 0) || (CultureInfo.InvariantCulture.CompareInfo.IndexOf(this._path, "//", CompareOptions.Ordinal) >= 0)));
        }

        public override bool IsClientConnected()
        {
            this._connectionPermission.Assert();
            return this._connection.Connected;
        }

        public override bool IsEntireEntityBodyIsPreloaded()
        {
            return (this._contentLength == this._preloadedContentLength);
        }

        private bool IsRequestForRestrictedDirectory()
        {
            string str = CultureInfo.InvariantCulture.TextInfo.ToLower(this._path);
            if (this._host.VirtualPath != "/")
            {
                str = str.Substring(this._host.VirtualPath.Length);
            }
            foreach (string str2 in restrictedDirs)
            {
                if (str.StartsWith(str2, StringComparison.Ordinal) && ((str.Length == str2.Length) || (str[str2.Length] == '/')))
                {
                    return true;
                }
            }
            return false;
        }

        public override string MapPath(string path)
        {
            string physicalPath = string.Empty;
            bool isClientScriptPath = false;
            if (((path == null) || (path.Length == 0)) || path.Equals("/"))
            {
                if (this._host.VirtualPath == "/")
                {
                    physicalPath = this._host.PhysicalPath;
                }
                else
                {
                    physicalPath = Environment.SystemDirectory;
                }
            }
            else if (this._host.IsVirtualPathAppPath(path))
            {
                physicalPath = this._host.PhysicalPath;
            }
            else if (this._host.IsVirtualPathInApp(path, out isClientScriptPath))
            {
                if (isClientScriptPath)
                {
                    physicalPath = this._host.PhysicalClientScriptPath + path.Substring(this._host.NormalizedClientScriptPath.Length);
                }
                else
                {
                    physicalPath = this._host.PhysicalPath + path.Substring(this._host.NormalizedVirtualPath.Length);
                }
            }
            else if (path.StartsWith("/", StringComparison.Ordinal))
            {
                physicalPath = this._host.PhysicalPath + path.Substring(1);
            }
            else
            {
                physicalPath = this._host.PhysicalPath + path;
            }
            physicalPath = physicalPath.Replace('/', '\\');
            if (!(!physicalPath.EndsWith(@"\", StringComparison.Ordinal) || physicalPath.EndsWith(@":\", StringComparison.Ordinal)))
            {
                physicalPath = physicalPath.Substring(0, physicalPath.Length - 1);
            }
            return physicalPath;
        }

        private void ParseHeaders()
        {
            this._knownRequestHeaders = new string[40];
            ArrayList list = new ArrayList();
            for (int i = 1; i < this._headerByteStrings.Count; i++)
            {
                string str = ((ByteString) this._headerByteStrings[i]).GetString();
                int index = str.IndexOf(':');
                if (index >= 0)
                {
                    string header = str.Substring(0, index).Trim();
                    string str3 = str.Substring(index + 1).Trim();
                    int knownRequestHeaderIndex = HttpWorkerRequest.GetKnownRequestHeaderIndex(header);
                    if (knownRequestHeaderIndex >= 0)
                    {
                        this._knownRequestHeaders[knownRequestHeaderIndex] = str3;
                    }
                    else
                    {
                        list.Add(header);
                        list.Add(str3);
                    }
                }
            }
            int num4 = list.Count / 2;
            this._unknownRequestHeaders = new string[num4][];
            int num5 = 0;
            for (int j = 0; j < num4; j++)
            {
                this._unknownRequestHeaders[j] = new string[] { (string) list[num5++], (string) list[num5++] };
            }
            if (this._headerByteStrings.Count > 1)
            {
                this._allRawHeaders = Encoding.UTF8.GetString(this._headerBytes, this._startHeadersOffset, this._endHeadersOffset - this._startHeadersOffset);
            }
            else
            {
                this._allRawHeaders = string.Empty;
            }
        }

        private void ParsePostedContent()
        {
            this._contentLength = 0;
            this._preloadedContentLength = 0;
            string s = this._knownRequestHeaders[11];
            if (s != null)
            {
                try
                {
                    this._contentLength = int.Parse(s, CultureInfo.InvariantCulture);
                }
                catch
                {
                }
            }
            if (this._headerBytes.Length > this._endHeadersOffset)
            {
                this._preloadedContentLength = this._headerBytes.Length - this._endHeadersOffset;
                if (this._preloadedContentLength > this._contentLength)
                {
                    this._preloadedContentLength = this._contentLength;
                }
                if (this._preloadedContentLength > 0)
                {
                    this._preloadedContent = new byte[this._preloadedContentLength];
                    Buffer.BlockCopy(this._headerBytes, this._endHeadersOffset, this._preloadedContent, 0, this._preloadedContentLength);
                }
            }
        }

        private void ParseRequestLine()
        {
            ByteString[] strArray = ((ByteString) this._headerByteStrings[0]).Split(' ');
            if (((strArray == null) || (strArray.Length < 2)) || (strArray.Length > 3))
            {
                this._connection.WriteErrorAndClose(400);
            }
            else
            {
                this._verb = strArray[0].GetString();
                ByteString str = strArray[1];
                this._url = str.GetString();
                byte[] buffer = new byte[] { 0xfd, 0xff };
                char ch = BitConverter.ToChar(buffer, 0);
                if (this._url.IndexOf(ch) >= 0)
                {
                    this._url = str.GetString(Encoding.Default);
                }
                if (strArray.Length == 3)
                {
                    this._prot = strArray[2].GetString();
                }
                else
                {
                    this._prot = "HTTP/1.0";
                }
                int index = str.IndexOf('?');
                if (index > 0)
                {
                    this._queryStringBytes = str.Substring(index + 1).GetBytes();
                }
                else
                {
                    this._queryStringBytes = new byte[0];
                }
                index = this._url.IndexOf('?');
                if (index > 0)
                {
                    this._path = this._url.Substring(0, index);
                    this._queryString = this._url.Substring(index + 1);
                }
                else
                {
                    this._path = this._url;
                    this._queryString = string.Empty;
                }
                if (this._path.IndexOf('%') >= 0)
                {
                    this._path = HttpUtility.UrlDecode(this._path, Encoding.UTF8);
                    index = this._url.IndexOf('?');
                    if (index >= 0)
                    {
                        this._url = this._path + this._url.Substring(index);
                    }
                    else
                    {
                        this._url = this._path;
                    }
                }
                int startIndex = this._path.LastIndexOf('.');
                int num3 = this._path.LastIndexOf('/');
                if (((startIndex >= 0) && (num3 >= 0)) && (startIndex < num3))
                {
                    int length = this._path.IndexOf('/', startIndex);
                    this._filePath = this._path.Substring(0, length);
                    this._pathInfo = this._path.Substring(length);
                }
                else
                {
                    this._filePath = this._path;
                    this._pathInfo = string.Empty;
                }
                this._pathTranslated = this.MapPath(this._filePath);
            }
        }

        private void PrepareResponse()
        {
            this._headersSent = false;
            this._responseStatus = 200;
            this._responseHeadersBuilder = new StringBuilder();
            this._responseBodyBytes = new ArrayList();
        }

        [AspNetHostingPermission(SecurityAction.Assert, Level=AspNetHostingPermissionLevel.Medium)]
        public void Process()
        {
            if (this.TryParseRequest())
            {
                if (((this._verb == "POST") && (this._contentLength > 0)) && (this._preloadedContentLength < this._contentLength))
                {
                    this._connection.Write100Continue();
                }
                if (!this._host.RequireAuthentication || this.TryNtlmAuthenticate())
                {
                    if (this._isClientScriptPath)
                    {
                        this._connection.WriteEntireResponseFromFile(this._host.PhysicalClientScriptPath + this._path.Substring(this._host.NormalizedClientScriptPath.Length), false);
                    }
                    else if (this.IsRequestForRestrictedDirectory())
                    {
                        this._connection.WriteErrorAndClose(0x193);
                    }
                    else if (!this.ProcessDefaultDocumentRequest())
                    {
                        this.PrepareResponse();
                        HttpRuntime.ProcessRequest(this);
                    }
                }
            }
        }

        private bool ProcessDefaultDocumentRequest()
        {
            if (this._verb == "GET")
            {
                string path = this._pathTranslated;
                if (this._pathInfo.Length > 0)
                {
                    path = this.MapPath(this._path);
                }
                if (!Directory.Exists(path))
                {
                    return false;
                }
                if (!this._path.EndsWith("/", StringComparison.Ordinal))
                {
                    string str2 = this._path + "/";
                    string extraHeaders = "Location: " + UrlEncodeRedirect(str2) + "\r\n";
                    string body = "<html><head><title>Object moved</title></head><body>\r\n<h2>Object moved to <a href='" + str2 + "'>here</a>.</h2>\r\n</body></html>\r\n";
                    this._connection.WriteEntireResponseFromString(0x12e, extraHeaders, body, false);
                    return true;
                }
                foreach (string str5 in defaultFileNames)
                {
                    string str6 = path + @"\" + str5;
                    if (File.Exists(str6))
                    {
                        this._path = this._path + str5;
                        this._filePath = this._path;
                        this._url = (this._queryString != null) ? (this._path + "?" + this._queryString) : this._path;
                        this._pathTranslated = str6;
                        return false;
                    }
                }
            }
            return false;
        }

        private bool ProcessDirectoryListingRequest()
        {
            if (this._verb != "GET")
            {
                return false;
            }
            string path = this._pathTranslated;
            if (this._pathInfo.Length > 0)
            {
                path = this.MapPath(this._path);
            }
            if (!Directory.Exists(path))
            {
                return false;
            }
            if (this._host.DisableDirectoryListing)
            {
                return false;
            }
            FileSystemInfo[] elements = null;
            try
            {
                elements = new DirectoryInfo(path).GetFileSystemInfos();
            }
            catch
            {
            }
            string str2 = null;
            if (this._path.Length > 1)
            {
                int length = this._path.LastIndexOf('/', this._path.Length - 2);
                str2 = (length > 0) ? this._path.Substring(0, length) : "/";
                if (!this._host.IsVirtualPathInApp(str2))
                {
                    str2 = null;
                }
            }
            this._connection.WriteEntireResponseFromString(200, "Content-type: text/html; charset=utf-8\r\n", Messages.FormatDirectoryListing(this._path, str2, elements), false);
            return true;
        }

        private void ReadAllHeaders()
        {
            this._headerBytes = null;
            while (this.TryReadAllHeaders() && (this._endHeadersOffset < 0))
            {
            }
        }

        public override int ReadEntityBody(byte[] buffer, int size)
        {
            int count = 0;
            this._connectionPermission.Assert();
            byte[] src = this._connection.ReadRequestBytes(size);
            if ((src != null) && (src.Length > 0))
            {
                count = src.Length;
                Buffer.BlockCopy(src, 0, buffer, 0, count);
            }
            return count;
        }

        private void Reset()
        {
            this._headerBytes = null;
            this._startHeadersOffset = 0;
            this._endHeadersOffset = 0;
            this._headerByteStrings = null;
            this._isClientScriptPath = false;
            this._verb = null;
            this._url = null;
            this._prot = null;
            this._path = null;
            this._filePath = null;
            this._pathInfo = null;
            this._pathTranslated = null;
            this._queryString = null;
            this._queryStringBytes = null;
            this._contentLength = 0;
            this._preloadedContentLength = 0;
            this._preloadedContent = null;
            this._allRawHeaders = null;
            this._unknownRequestHeaders = null;
            this._knownRequestHeaders = null;
            this._specialCaseStaticFileHeaders = false;
        }

        public override void SendCalculatedContentLength(int contentLength)
        {
            if (!this._headersSent)
            {
                this._responseHeadersBuilder.Append("Content-Length: ");
                this._responseHeadersBuilder.Append(contentLength.ToString(CultureInfo.InvariantCulture));
                this._responseHeadersBuilder.Append("\r\n");
            }
        }

        public override void SendKnownResponseHeader(int index, string value)
        {
            if (!this._headersSent)
            {
                switch (index)
                {
                    case 1:
                    case 2:
                    case 0x1a:
                        return;

                    case 0x12:
                    case 0x13:
                        if (this._specialCaseStaticFileHeaders)
                        {
                            return;
                        }
                        break;

                    case 20:
                        if (value == "bytes")
                        {
                            this._specialCaseStaticFileHeaders = true;
                            return;
                        }
                        break;
                }
                this._responseHeadersBuilder.Append(HttpWorkerRequest.GetKnownResponseHeaderName(index));
                this._responseHeadersBuilder.Append(": ");
                this._responseHeadersBuilder.Append(value);
                this._responseHeadersBuilder.Append("\r\n");
            }
        }

        public override void SendResponseFromFile(IntPtr handle, long offset, long length)
        {
            if (length != 0L)
            {
                FileStream f = null;
                try
                {
                    SafeFileHandle handle2 = new SafeFileHandle(handle, false);
                    f = new FileStream(handle2, FileAccess.Read);
                    this.SendResponseFromFileStream(f, offset, length);
                }
                finally
                {
                    if (f != null)
                    {
                        f.Close();
                        f = null;
                    }
                }
            }
        }

        public override void SendResponseFromFile(string filename, long offset, long length)
        {
            if (length != 0L)
            {
                FileStream f = null;
                try
                {
                    f = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                    this.SendResponseFromFileStream(f, offset, length);
                }
                finally
                {
                    if (f != null)
                    {
                        f.Close();
                    }
                }
            }
        }

        private void SendResponseFromFileStream(FileStream f, long offset, long length)
        {
            long num = f.Length;
            if (length == -1L)
            {
                length = num - offset;
            }
            if (((length != 0L) && (offset >= 0L)) && (length <= (num - offset)))
            {
                if (offset > 0L)
                {
                    f.Seek(offset, SeekOrigin.Begin);
                }
                if (length <= 0x10000L)
                {
                    byte[] buffer = new byte[(int) length];
                    int num2 = f.Read(buffer, 0, (int) length);
                    this.SendResponseFromMemory(buffer, num2);
                }
                else
                {
                    byte[] buffer2 = new byte[0x10000];
                    int num3 = (int) length;
                    while (num3 > 0)
                    {
                        int count = (num3 < 0x10000) ? num3 : 0x10000;
                        int num5 = f.Read(buffer2, 0, count);
                        this.SendResponseFromMemory(buffer2, num5);
                        num3 -= num5;
                        if ((num3 > 0) && (num5 > 0))
                        {
                            this.FlushResponse(false);
                        }
                    }
                }
            }
        }

        public override void SendResponseFromMemory(byte[] data, int length)
        {
            if (length > 0)
            {
                byte[] dst = new byte[length];
                Buffer.BlockCopy(data, 0, dst, 0, length);
                this._responseBodyBytes.Add(dst);
            }
        }

        public override void SendStatus(int statusCode, string statusDescription)
        {
            this._responseStatus = statusCode;
        }

        public override void SendUnknownResponseHeader(string name, string value)
        {
            if (!this._headersSent)
            {
                this._responseHeadersBuilder.Append(name);
                this._responseHeadersBuilder.Append(": ");
                this._responseHeadersBuilder.Append(value);
                this._responseHeadersBuilder.Append("\r\n");
            }
        }

        private void SkipAllPostedContent()
        {
            if ((this._contentLength > 0) && (this._preloadedContentLength < this._contentLength))
            {
                byte[] buffer;
                for (int i = this._contentLength - this._preloadedContentLength; i > 0; i -= buffer.Length)
                {
                    buffer = this._connection.ReadRequestBytes(i);
                    if ((buffer == null) || (buffer.Length == 0))
                    {
                        break;
                    }
                }
            }
        }

        [SecurityPermission(SecurityAction.Assert, ControlPrincipal=true), SecurityPermission(SecurityAction.Assert, UnmanagedCode=true)]
        private bool TryNtlmAuthenticate()
        {
            //try
            //{
            //    using (NtlmAuth auth = new NtlmAuth())
            //    {
            //        do
            //        {
            //            string blobString = null;
            //            string extraHeaders = this._knownRequestHeaders[0x18];
            //            if ((extraHeaders != null) && extraHeaders.StartsWith("NTLM ", StringComparison.Ordinal))
            //            {
            //                blobString = extraHeaders.Substring(5);
            //            }
            //            if (blobString != null)
            //            {
            //                if (!auth.Authenticate(blobString))
            //                {
            //                    this._connection.WriteErrorAndClose(0x193);
            //                    return false;
            //                }
            //                if (auth.Completed)
            //                {
            //                    goto Label_00CC;
            //                }
            //                extraHeaders = "WWW-Authenticate: NTLM " + auth.Blob + "\r\n";
            //            }
            //            else
            //            {
            //                extraHeaders = "WWW-Authenticate: NTLM\r\n";
            //            }
            //            this.SkipAllPostedContent();
            //            this._connection.WriteErrorWithExtraHeadersAndKeepAlive(0x191, extraHeaders);
            //        }
            //        while (this.TryParseRequest());
            //        return false;
            //    Label_00CC:
            //        if (this._host.GetProcessSID() != auth.SID)
            //        {
            //            this._connection.WriteErrorAndClose(0x193);
            //            return false;
            //        }
            //    }
            //}
            //catch
            //{
            //    try
            //    {
            //        this._connection.WriteErrorAndClose(500);
            //    }
            //    catch
            //    {
            //    }
            //    return false;
            //}
            return true;
        }

        private bool TryParseRequest()
        {
            this.Reset();
            this.ReadAllHeaders();
            if (!this._connection.IsLocal)
            {
                this._connection.WriteErrorAndClose(0x193);
                return false;
            }
            if (((this._headerBytes == null) || (this._endHeadersOffset < 0)) || ((this._headerByteStrings == null) || (this._headerByteStrings.Count == 0)))
            {
                this._connection.WriteErrorAndClose(400);
                return false;
            }
            this.ParseRequestLine();
            if (this.IsBadPath())
            {
                this._connection.WriteErrorAndClose(400);
                return false;
            }
            if (!this._host.IsVirtualPathInApp(this._path, out this._isClientScriptPath))
            {
                this._connection.WriteErrorAndClose(0x194);
                return false;
            }
            this.ParseHeaders();
            this.ParsePostedContent();
            return true;
        }

        private bool TryReadAllHeaders()
        {
            byte[] src = this._connection.ReadRequestBytes(0x8000);
            if ((src == null) || (src.Length == 0))
            {
                return false;
            }
            if (this._headerBytes != null)
            {
                int num = src.Length + this._headerBytes.Length;
                if (num > 0x8000)
                {
                    return false;
                }
                byte[] dst = new byte[num];
                Buffer.BlockCopy(this._headerBytes, 0, dst, 0, this._headerBytes.Length);
                Buffer.BlockCopy(src, 0, dst, this._headerBytes.Length, src.Length);
                this._headerBytes = dst;
            }
            else
            {
                this._headerBytes = src;
            }
            this._startHeadersOffset = -1;
            this._endHeadersOffset = -1;
            this._headerByteStrings = new ArrayList();
            ByteParser parser = new ByteParser(this._headerBytes);
            while (true)
            {
                ByteString str = parser.ReadLine();
                if (str == null)
                {
                    break;
                }
                if (this._startHeadersOffset < 0)
                {
                    this._startHeadersOffset = parser.CurrentOffset;
                }
                if (str.IsEmpty)
                {
                    this._endHeadersOffset = parser.CurrentOffset;
                    break;
                }
                this._headerByteStrings.Add(str);
            }
            return true;
        }

        private static string UrlEncodeRedirect(string path)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(path);
            int length = bytes.Length;
            int num2 = 0;
            for (int i = 0; i < length; i++)
            {
                if ((bytes[i] & 0x80) != 0)
                {
                    num2++;
                }
            }
            if (num2 > 0)
            {
                byte[] buffer2 = new byte[length + (num2 * 2)];
                int num4 = 0;
                for (int j = 0; j < length; j++)
                {
                    byte num6 = bytes[j];
                    if ((num6 & 0x80) == 0)
                    {
                        buffer2[num4++] = num6;
                    }
                    else
                    {
                        buffer2[num4++] = 0x25;
                        buffer2[num4++] = (byte) IntToHex[(num6 >> 4) & 15];
                        buffer2[num4++] = (byte) IntToHex[num6 & 15];
                    }
                }
                path = Encoding.ASCII.GetString(buffer2);
            }
            if (path.IndexOf(' ') >= 0)
            {
                path = path.Replace(" ", "%20");
            }
            return path;
        }
    }
}

