using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Hosting;

namespace AspNetServer
{
    class Program
    {
        static void Main(string[] args)
        {
            int port;
            string dir = Directory.GetCurrentDirectory();
            if(args.Length==0 || !int.TryParse(args[0],out port))
            {
                port = 80;
            }

            InitHostFile(dir);
            SimpleHost host= (SimpleHost) ApplicationHost.CreateApplicationHost(typeof (SimpleHost), "/", dir);
            host.Config("/", dir);

            WebServer server = new WebServer(host, port);
            server.Start();
            OpenUrl("http://127.0.0.1/default.aspx");
        }

        //需要拷贝执行文件 才能创建ASP.NET应用程序域
        private static void InitHostFile(string dir)
        {
            string path = Path.Combine(dir, "bin");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            string source = Assembly.GetExecutingAssembly().Location;
            string target = path + "/" + Assembly.GetExecutingAssembly().GetName().Name + ".exe";
            if(File.Exists(target))
                File.Delete(target);
            File.Copy(source, target);
        }

        public static void OpenUrl(string url = "")
        {
            RegistryKey key = Registry.ClassesRoot.OpenSubKey(@"http\shell\open\command\");
            string s = key.GetValue("").ToString();

            Regex reg = new Regex("\"([^\"]+)\"");
            MatchCollection matchs = reg.Matches(s);

            string filename = "";
            if (matchs.Count > 0)
            {
                filename = matchs[0].Groups[1].Value;
                System.Diagnostics.Process.Start(filename, url);
            }
        }
    }
}
