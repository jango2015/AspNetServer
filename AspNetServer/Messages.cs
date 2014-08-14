namespace AspNetServer
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Web;

    internal class Messages
    {
        private const string _dirListingDirFormat = "{0,38:dddd, MMMM dd, yyyy hh:mm tt}        &lt;dir&gt; <A href=\"{1}/\">{2}</A>\r\n";
        private const string _dirListingFileFormat = "{0,38:dddd, MMMM dd, yyyy hh:mm tt} {1,12:n0} <A href=\"{2}\">{3}</A>\r\n";
        private const string _dirListingFormat1 = "<html>\r\n    <head>\r\n    <title>{0}</title>\r\n";
        private const string _dirListingFormat2 = "    </head>\r\n    <body bgcolor=\"white\">\r\n\r\n    <h2> <i>{0}</i> </h2></span>\r\n\r\n            <hr width=100% size=1 color=silver>\r\n\r\n<PRE>\r\n";
        private const string _dirListingParentFormat = "<A href=\"{0}\">[To Parent Directory]</A>\r\n\r\n";
        private static string _dirListingTail = ("</PRE>\r\n            <hr width=100% size=1 color=silver>\r\n\r\n              <b>Dev—<a href=\"http://waw.cnblogs.com\" target=\"_black\">吴艾伟</a>:</b>&nbsp;{1} " + VersionString + "</body>\r\n</html>\r\n");
        private const string _httpErrorFormat1 = "<html>\r\n    <head>\r\n        <title>{0}</title>\r\n";
        private static string _httpErrorFormat2 = ("    </head>\r\n    <body bgcolor=\"white\">\r\n\r\n            <span><h1>{0}<hr width=100% size=1 color=silver></h1>\r\n\r\n            <h2> <i>{1}</i> </h2></span>\r\n\r\n            <hr width=100% size=1 color=silver>\r\n\r\n            <b>{2}:</b>&nbsp;{3} " + VersionString + "\r\n\r\n            \r\n\r\n    </body>\r\n</html>\r\n");
        private const string _httpStyle = "        <style>\r\n        \tbody {font-family:\"宋体\";font-weight:normal;font-size: 12pt;color:black;} \r\n        \tp {font-family:\"宋体\";font-weight:normal;color:black;margin-top: -5px}\r\n        \tb {font-family:\"宋体\";font-weight:bold;color:black;margin-top: -5px}\r\n        \th1 { font-family:\"宋体\";font-weight:normal;font-size:18pt;color:red }\r\n        \th2 { font-family:\"宋体\";font-weight:normal;font-size:14pt;color:maroon }\r\n        \tpre {font-family:\"Lucida Console\";font-size: 12pt}\r\n        \t.marker {font-weight: bold; color: black;text-decoration: none;}\r\n        \t.version {color: gray;}\r\n        \t.error {margin-bottom: 10px;}\r\n        \t.expandable { text-decoration:underline; font-weight:bold; color:navy; cursor:hand; }\r\n        </style>\r\n";
        public static string VersionString = typeof(Server).Assembly.GetName().Version.ToString();

        public static string FormatDirectoryListing(string dirPath, string parentPath, FileSystemInfo[] elements)
        {
            StringBuilder builder = new StringBuilder();
            string str = string.Format("目录清单 -- {0}", dirPath);
            string str2 = "Dev—吴艾伟";
            string str3 = "AspNet4.0网站运行助手 V2.2";
            string str4 = string.Format(CultureInfo.InvariantCulture, _dirListingTail, new object[] { str2, str3 });
            builder.Append(string.Format(CultureInfo.InvariantCulture, "<html>\r\n    <head>\r\n    <title>{0}</title>\r\n", new object[] { str }));
            builder.Append("        <style>\r\n        \tbody {font-family:\"宋体\";font-weight:normal;font-size: 12pt;color:black;} \r\n        \tp {font-family:\"宋体\";font-weight:normal;color:black;margin-top: -5px}\r\n        \tb {font-family:\"宋体\";font-weight:bold;color:black;margin-top: -5px}\r\n        \th1 { font-family:\"宋体\";font-weight:normal;font-size:18pt;color:red }\r\n        \th2 { font-family:\"宋体\";font-weight:normal;font-size:14pt;color:maroon }\r\n        \tpre {font-family:\"Lucida Console\";font-size: 12pt}\r\n        \t.marker {font-weight: bold; color: black;text-decoration: none;}\r\n        \t.version {color: gray;}\r\n        \t.error {margin-bottom: 10px;}\r\n        \t.expandable { text-decoration:underline; font-weight:bold; color:navy; cursor:hand; }\r\n        </style>\r\n");
            builder.Append(string.Format(CultureInfo.InvariantCulture, "    </head>\r\n    <body bgcolor=\"white\">\r\n\r\n    <h2> <i>{0}</i> </h2></span>\r\n\r\n            <hr width=100% size=1 color=silver>\r\n\r\n<PRE>\r\n", new object[] { str }));
            if (parentPath != null)
            {
                if (!parentPath.EndsWith("/", StringComparison.Ordinal))
                {
                    parentPath = parentPath + "/";
                }
                builder.Append(string.Format(CultureInfo.InvariantCulture, "<A href=\"{0}\">[To Parent Directory]</A>\r\n\r\n", new object[] { parentPath }));
            }
            if (elements != null)
            {
                for (int i = 0; i < elements.Length; i++)
                {
                    if (elements[i] is FileInfo)
                    {
                        FileInfo info = (FileInfo) elements[i];
                        builder.Append(string.Format(CultureInfo.InvariantCulture, "{0,38:dddd, MMMM dd, yyyy hh:mm tt} {1,12:n0} <A href=\"{2}\">{3}</A>\r\n", new object[] { info.LastWriteTime, info.Length, info.Name, info.Name }));
                    }
                    else if (elements[i] is DirectoryInfo)
                    {
                        DirectoryInfo info2 = (DirectoryInfo) elements[i];
                        builder.Append(string.Format(CultureInfo.InvariantCulture, "{0,38:dddd, MMMM dd, yyyy hh:mm tt}        &lt;dir&gt; <A href=\"{1}/\">{2}</A>\r\n", new object[] { info2.LastWriteTime, info2.Name, info2.Name }));
                    }
                }
            }
            builder.Append(str4);
            return builder.ToString();
        }

        public static string FormatErrorMessageBody(int statusCode, string appName)
        {
            string statusDescription = HttpWorkerRequest.GetStatusDescription(statusCode);
            string str2 = string.Format("服务器出错 发生在 '{0}' Web应用程序中.", appName);
            string str3 = string.Format("HTTP错误 {0} - {1}.", statusCode, statusDescription);
            string str4 = "Dev—吴艾伟";
            string str5 = "AspNet4.0网站运行助手 V2.2";
            return (string.Format(CultureInfo.InvariantCulture, "<html>\r\n    <head>\r\n        <title>{0}</title>\r\n", new object[] { statusDescription }) + "        <style>\r\n        \tbody {font-family:\"宋体\";font-weight:normal;font-size: 12pt;color:black;} \r\n        \tp {font-family:\"宋体\";font-weight:normal;color:black;margin-top: -5px}\r\n        \tb {font-family:\"宋体\";font-weight:bold;color:black;margin-top: -5px}\r\n        \th1 { font-family:\"宋体\";font-weight:normal;font-size:18pt;color:red }\r\n        \th2 { font-family:\"宋体\";font-weight:normal;font-size:14pt;color:maroon }\r\n        \tpre {font-family:\"宋体\";font-size: 12pt}\r\n        \t.marker {font-weight: bold; color: black;text-decoration: none;}\r\n        \t.version {color: gray;}\r\n        \t.error {margin-bottom: 10px;}\r\n        \t.expandable { text-decoration:underline; font-weight:bold; color:navy; cursor:hand; }\r\n        </style>\r\n" + string.Format(CultureInfo.InvariantCulture, _httpErrorFormat2, new object[] { str2, str3, str4, str5 }));
        }
    }
}

