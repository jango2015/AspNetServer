using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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

            var virtualPath = "/";
            Server server = new Server(port, virtualPath, dir);
            server.Start();

            OpenUrl("http://127.0.0.1/default.aspx");

            Console.WriteLine("please press Entry to exit.");
            var key = Console.ReadKey();
            while ( key.Key != ConsoleKey.Enter)
            {
                Thread.Sleep(1000);
            }
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
