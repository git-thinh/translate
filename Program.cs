using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Linq;
using Newtonsoft.Json;
using System.IO;
using System.Net;
using System.Web;

class Program
{
    const int __PORT_WRITE = 1000;
    const int __PORT_READ = 1001;
    const string __SUBCRIBE_IN = "__TRANSLATE_IN";
    const string __SUBCRIBE_OUT = "__TRANSLATE_OUT";
    static RedisBase m_subcriber;
    static bool __running = true;

    static void __transTextGoogle01(string requestId, string input)
    {
        GoogleTranslate01.TranslateAsync(input, "en", "vi", string.Empty, (success, result, type) =>
        {
            //SetResult(result, type);
            //IsBusy(false);
            //Console.WriteLine(text + "(" + type + "): " + result);
            //Trace.WriteLine(text + "(" + type + "): " + result);
            ;
        });

        //var redis = new RedisBase(new RedisSetting(REDIS_TYPE.ONLY_WRITE, __PORT_WRITE));
        //var cmd = COMMANDS.TRANSLATE_TEXT_GOOGLE_01.ToString();
        //cmd = cmd.Substring(0, cmd.Length - 3);
        //redis.HSET("_TRANSLATE", input, result);
        //redis.ReplyRequest(requestId, cmd, 1, "01", input, result);
    }

    static void __executeTaskHttp(Tuple<string, COMMANDS, string> data)
    {
        string requestId = data.Item1, input = data.Item3;
        COMMANDS cmd = data.Item2;
        switch (cmd)
        {
            case COMMANDS.TRANSLATE_TEXT_GOOGLE_01:
                __transTextGoogle01(requestId, input);
                break;
        }
    }

    #region [ MAIN ]

    static void __executeTaskTcp(Tuple<string, byte[]> data)
    {
        if (data == null || data.Item2 == null || data.Item2.Length < 39) return;
        var buf = data.Item2;
        string requestId = Encoding.ASCII.GetString(buf, 0, 36);
        var cmd = (COMMANDS)((int)buf[36]);
        string text = Encoding.UTF8.GetString(buf, 37, buf.Length - 37);
        __executeTaskHttp(new Tuple<string, COMMANDS, string>(requestId, cmd, text));
    }

    static WebServer _http;
    static int _http_port = __freeTcpPort();
    static void __startApp()
    {
        string uri = string.Format("http://127.0.0.1:{0}/", _http_port);
        _http = new WebServer(__executeTaskHttp);
        _http.Start(uri);
        m_subcriber = new RedisBase(new RedisSetting(REDIS_TYPE.ONLY_SUBCRIBE, __PORT_READ));
        m_subcriber.PSUBSCRIBE(__SUBCRIBE_IN);
        var bs = new List<byte>();
        while (__running)
        {
            if (!m_subcriber.m_stream.DataAvailable)
            {
                if (bs.Count > 0)
                {
                    var buf = m_subcriber.__getBodyPublish(bs.ToArray(), __SUBCRIBE_IN);
                    bs.Clear();
                    if (buf != null)
                        new Thread(new ParameterizedThreadStart((o) =>
                        __executeTaskTcp((Tuple<string, byte[]>)o))).Start(buf);
                }
                Thread.Sleep(100);
                continue;
            }
            byte b = (byte)m_subcriber.m_stream.ReadByte();
            bs.Add(b);
        }
    }

    static void __stopApp()
    {
        __running = false;
        _http.Stop();
    }

    // FOR SETTING OF WINDOWS SERVICE

    static Thread __threadWS = null;
    static void Main(string[] args)
    {
        if (Environment.UserInteractive)
        {
            Console.Title = string.Format("{0} - {1}", __SUBCRIBE_IN, _http_port);
            StartOnConsoleApp(args);
            Console.WriteLine("Press any key to stop...");
            Console.ReadKey(true);
            Stop();
        }
        else using (var service = new MyService())
                ServiceBase.Run(service);
    }

    public static void StartOnConsoleApp(string[] args) => __startApp();
    public static void StartOnWindowService(string[] args)
    {
        __threadWS = new Thread(new ThreadStart(() => __startApp()));
        __threadWS.IsBackground = true;
        __threadWS.Start();
    }

    public static void Stop()
    {
        __stopApp();
        if (__threadWS != null) __threadWS.Abort();
    }
    static int __freeTcpPort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
    #endregion;
}

