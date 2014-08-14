namespace AspNetServer
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Web;

    internal sealed class Connection : MarshalByRefObject
    {
        private static string _localServerIP;
        private Server _server;
        private Socket _socket;

        internal Connection(Server server, Socket socket)
        {
            this._server = server;
            this._socket = socket;
        }

        internal void Close()
        {
            try
            {
                this._socket.Shutdown(SocketShutdown.Both);
                this._socket.Close();
            }
            catch
            {
            }
            finally
            {
                this._socket = null;
            }
        }

        private string GetErrorResponseBody(int statusCode, string message)
        {
            string str = Messages.FormatErrorMessageBody(statusCode, this._server.VirtualPath);
            if ((message != null) && (message.Length > 0))
            {
                str = str + "\r\n<!--\r\n" + message + "\r\n-->";
            }
            return str;
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }

        private static string MakeContentTypeHeader(string fileName)
        {
            string str = null;
            FileInfo info = new FileInfo(fileName);
            switch (info.Extension.ToLowerInvariant())
            {
                case ".bmp":
                    str = "image/bmp";
                    break;

                case ".css":
                    str = "text/css";
                    break;

                case ".gif":
                    str = "image/gif";
                    break;

                case ".ico":
                    str = "image/x-icon";
                    break;

                case ".htm":
                case ".html":
                    str = "text/html";
                    break;

                case ".jpe":
                case ".jpeg":
                case ".jpg":
                    str = "image/jpeg";
                    break;

                case ".js":
                    str = "application/x-javascript";
                    break;
            }
            if (str == null)
            {
                return null;
            }
            return ("Content-Type: " + str + "\r\n");
        }

        private static string MakeResponseHeaders(int statusCode, string moreHeaders, int contentLength, bool keepAlive)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(string.Concat(new object[] { "HTTP/1.1 ", statusCode, " ", HttpWorkerRequest.GetStatusDescription(statusCode), "\r\n" }));
            builder.Append("Server: ASP.NET Development Server/" + Messages.VersionString + "\r\n");
            builder.Append("Date: " + DateTime.Now.ToUniversalTime().ToString("R", DateTimeFormatInfo.InvariantInfo) + "\r\n");
            if (contentLength >= 0)
            {
                builder.Append("Content-Length: " + contentLength + "\r\n");
            }
            if (moreHeaders != null)
            {
                builder.Append(moreHeaders);
            }
            if (!keepAlive)
            {
                builder.Append("Connection: Close\r\n");
            }
            builder.Append("\r\n");
            return builder.ToString();
        }

        internal byte[] ReadRequestBytes(int maxBytes)
        {
            try
            {
                if (this.WaitForRequestBytes() == 0)
                {
                    return null;
                }
                int available = this._socket.Available;
                if (available > maxBytes)
                {
                    available = maxBytes;
                }
                int count = 0;
                byte[] buffer = new byte[available];
                if (available > 0)
                {
                    count = this._socket.Receive(buffer, 0, available, SocketFlags.None);
                }
                if (count < available)
                {
                    byte[] dst = new byte[count];
                    if (count > 0)
                    {
                        Buffer.BlockCopy(buffer, 0, dst, 0, count);
                    }
                    buffer = dst;
                }
                return buffer;
            }
            catch
            {
                return null;
            }
        }

        internal int WaitForRequestBytes()
        {
            int available = 0;
            try
            {
                if (this._socket.Available == 0)
                {
                    this._socket.Poll(0x186a0, SelectMode.SelectRead);
                    if ((this._socket.Available == 0) && this._socket.Connected)
                    {
                        this._socket.Poll(0x1c9c380, SelectMode.SelectRead);
                    }
                }
                available = this._socket.Available;
            }
            catch
            {
            }
            return available;
        }

        internal void Write100Continue()
        {
            this.WriteEntireResponseFromString(100, null, null, true);
        }

        internal void WriteBody(byte[] data, int offset, int length)
        {
            try
            {
                this._socket.Send(data, offset, length, SocketFlags.None);
            }
            catch (SocketException)
            {
            }
        }

        internal void WriteEntireResponseFromFile(string fileName, bool keepAlive)
        {
            if (!System.IO.File.Exists(fileName))
            {
                this.WriteErrorAndClose(0x194);
            }
            else
            {
                string moreHeaders = MakeContentTypeHeader(fileName);
                if (moreHeaders == null)
                {
                    this.WriteErrorAndClose(0x193);
                }
                else
                {
                    bool flag = false;
                    FileStream stream = null;
                    try
                    {
                        stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                        int length = (int) stream.Length;
                        byte[] buffer = new byte[length];
                        int contentLength = stream.Read(buffer, 0, length);
                        string s = MakeResponseHeaders(200, moreHeaders, contentLength, keepAlive);
                        this._socket.Send(Encoding.UTF8.GetBytes(s));
                        this._socket.Send(buffer, 0, contentLength, SocketFlags.None);
                        flag = true;
                    }
                    catch (SocketException)
                    {
                    }
                    finally
                    {
                        if (!(keepAlive && flag))
                        {
                            this.Close();
                        }
                        if (stream != null)
                        {
                            stream.Close();
                        }
                    }
                }
            }
        }

        internal void WriteEntireResponseFromString(int statusCode, string extraHeaders, string body, bool keepAlive)
        {
            try
            {
                int contentLength = (body != null) ? Encoding.UTF8.GetByteCount(body) : 0;
                string str = MakeResponseHeaders(statusCode, extraHeaders, contentLength, keepAlive);
                this._socket.Send(Encoding.UTF8.GetBytes(str + body));
            }
            catch (SocketException)
            {
            }
            finally
            {
                if (!keepAlive)
                {
                    this.Close();
                }
            }
        }

        internal void WriteErrorAndClose(int statusCode)
        {
            this.WriteErrorAndClose(statusCode, null);
        }

        internal void WriteErrorAndClose(int statusCode, string message)
        {
            this.WriteEntireResponseFromString(statusCode, "Content-type:text/html;charset=utf-8\r\n", this.GetErrorResponseBody(statusCode, message), false);
        }

        internal void WriteErrorWithExtraHeadersAndKeepAlive(int statusCode, string extraHeaders)
        {
            this.WriteEntireResponseFromString(statusCode, extraHeaders, this.GetErrorResponseBody(statusCode, null), true);
        }

        internal void WriteHeaders(int statusCode, string extraHeaders)
        {
            string s = MakeResponseHeaders(statusCode, extraHeaders, -1, false);
            try
            {
                this._socket.Send(Encoding.UTF8.GetBytes(s));
            }
            catch (SocketException)
            {
            }
        }

        internal bool Connected
        {
            get
            {
                return this._socket.Connected;
            }
        }

        internal bool IsLocal
        {
            get
            {
                string remoteIP = this.RemoteIP;
                return (remoteIP.Equals("127.0.0.1") || LocalServerIP.Equals(remoteIP));
            }
        }

        internal string LocalIP
        {
            get
            {
                IPEndPoint localEndPoint = (IPEndPoint) this._socket.LocalEndPoint;
                if ((localEndPoint != null) && (localEndPoint.Address != null))
                {
                    return localEndPoint.Address.ToString();
                }
                return "127.0.0.1";
            }
        }

        private static string LocalServerIP
        {
            get
            {
                if (_localServerIP == null)
                {
                    _localServerIP = Dns.GetHostEntry(Environment.MachineName).AddressList[0].ToString();
                }
                return _localServerIP;
            }
        }

        internal string RemoteIP
        {
            get
            {
                IPEndPoint remoteEndPoint = (IPEndPoint) this._socket.RemoteEndPoint;
                if ((remoteEndPoint != null) && (remoteEndPoint.Address != null))
                {
                    return remoteEndPoint.Address.ToString();
                }
                return "127.0.0.1";
            }
        }
    }
}

