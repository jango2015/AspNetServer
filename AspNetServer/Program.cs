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
using Topshelf;

namespace AspNetServer
{
    public class AspNetService
    {
        private Server server;

        public void Start()
        {
            if (server == null)
            {
                var port = 80;
                string dir = Directory.GetCurrentDirectory();
                var virtualPath = "/";
                server = new Server(port, virtualPath, dir);
                server.Start();
            }
        }

        public void Stop()
        {
            if (server != null)
            {
                server.Stop();
            }
        }

    }

    class Program
    {
        static void Main(string[] args)
        {
            //int port;
            //string dir = Directory.GetCurrentDirectory();
            //if(args.Length==0 || !int.TryParse(args[0],out port))
            //{
            //    port = 80;
            //}

            //var virtualPath = "/";
            //Server server = new Server(port, virtualPath, dir);
            //server.Start();
            //server.Stop();

            OpenUrl("http://127.0.0.1/default.aspx");

            StartService();

            

            //Console.WriteLine("please press Entry to exit.");
            //var key = Console.ReadKey();
            //while ( key.Key != ConsoleKey.Enter)
            //{
            //    Thread.Sleep(1000);
            //}
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

        public static void StartService()
        {
            var serviceName = "AspNetServer";

            var host = HostFactory.New(x =>
            {
                x.Service<AspNetService>(s =>
                {
                    s.ConstructUsing(name => new AspNetService());
                    s.WhenStarted((t) => t.Start());
                    s.WhenStopped((t) => t.Stop());
                });

                x.RunAsLocalSystem();
                x.SetDescription(serviceName);
                x.SetDisplayName(serviceName);
                x.SetServiceName(serviceName);
            });

            host.Run();
        }
    }
}
