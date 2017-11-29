using Shadowsocks.Controller;
using Shadowsocks.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace Shadowsocks.Extension
{
    internal static class AutoPassword
    {
        private static Timer _timer;
        private static ShadowsocksController _controller;
        static AutoPassword()
        {
            // 创建计时器但不启动
            // 确保 _timer 在线程池调用 PasswordCheck 之前引用该计时器
            _timer = new Timer(PasswordCheck, null, Timeout.Infinite, Timeout.Infinite);
            // 现在 _timer 已被赋值，可以启动计时器了
            // 现在在 PasswordCheck 中调用 _timer 保证不会抛出 NullReferenceException
            _timer.Change(0, Timeout.Infinite);
            Logging.Info("开启 ishadowsocks 监听");
        }

        static void DoUpdate(string msg)
        {
            Logging.Info(msg);
            UpdateConfig();
        }

        static void PasswordCheck(object obj)
        {
            if (DateTime.Now.Minute == 0 || DateTime.Now.Minute == 1 || DateTime.Now.Minute == 2 || DateTime.Now.Minute == 3)
            {
                DoUpdate("整点更新密码");
            }
            _timer.Change(1000 * 40, Timeout.Infinite);  // 30s 检查一次，当为整点时，去读取服务器端更新的密码
        }

        static void UpdateConfig()
        {
            var config = Configuration.Load();
            var passwords = GetPassword();
            bool shouldUpdate = false;
            foreach (var serverInfo in config.configs)
            {
                if (passwords.ContainsKey(serverInfo.server))
                {
                    if (serverInfo.password != passwords[serverInfo.server])
                    {
                        shouldUpdate = true;
                        serverInfo.password = passwords[serverInfo.server];
                    }
                }
            }
            if (shouldUpdate)
            {
                _controller.Stop();
                Configuration.Save(config);
                Logging.Info("密码改变，更新成功");
                // 将会重新载入配置文件
                _controller.Start();
            }
            else
            {
                Logging.Info("密码未变，无需更新");
            }
        }

        static Dictionary<String, String> GetPassword()
        {
            Dictionary<String, String> res = new Dictionary<string, string>();
            GetPasswordA(res);
            GetPasswordB(res);
            return res;
        }

        static void GetPasswordA(Dictionary<String, String> res)
        {
            Regex usa = new Regex(@"<h4>A密码:(?<Password>\d+)</h4>");
            Regex hka = new Regex(@"<h4>B密码:(?<Password>\d+)</h4>");
            Regex jpa = new Regex(@"<h4>C密码:(?<Password>\d+)</h4>");
            WebRequest request = HttpWebRequest.Create("http://www.ishadowsocks.mobi/?timestamp=" + DateTime.Now.Ticks);
            WebResponse response = null;
            try
            {
                using (response = request.GetResponse())
                {
                    using (var stream = response.GetResponseStream())
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            var tp = reader.ReadToEnd();
                            Match match = usa.Match(tp);
                            string password = "";
                            if (match.Success)
                            {
                                password = match.Groups["Password"].Value;
                                Logging.Info("获取 A.SSX.HOST 密码：" + password);
                                res.Add("A.SSX.HOST", password);
                            }
                            match = hka.Match(tp);
                            if (match.Success)
                            {
                                password = match.Groups["Password"].Value;
                                Logging.Info("获取 B.SSX.HOST 密码：" + password);
                                res.Add("B.SSX.HOST", password);
                            }
                            match = jpa.Match(tp);
                            if (match.Success)
                            {
                                password = match.Groups["Password"].Value;
                                Logging.Info("获取 C.SSX.HOST 密码：" + password);
                                res.Add("C.SSX.HOST", password);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (response != null)
                {
                    response.Close();
                }
                Logging.Info(String.Format("GetPasswordA Error：", ex.StackTrace));
            }
        }

        static void GetPasswordB(Dictionary<String, String> res)
        {
            string[] images = { "server01.png", "server02.png", "server03.png" };

            foreach (string image in images)
            {
                WebRequest request = HttpWebRequest.Create(String.Format("http://www.shadowsocks8.com/images/{0}?timestamp={1}", image, DateTime.Now.Ticks));
                WebResponse response = null;
                try
                {
                    response = request.GetResponse();
                    using (Stream stream = response.GetResponseStream())
                    {
                        using (Bitmap fullImage = (Bitmap)Bitmap.FromStream(stream))
                        {
                            var source = new BitmapLuminanceSource(fullImage);
                            var bitmap = new BinaryBitmap(new HybridBinarizer(source));
                            QRCodeReader reader = new QRCodeReader();
                            var result = reader.decode(bitmap);
                            if (result != null)
                            {
                                //var sv = new Server(result.Text);  // ssURL
                                var sv = new Server();
                                res.Add(sv.server, sv.password);
                                Logging.Info(String.Format("获取 {0} 密码：{1}", sv.server, sv.password));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (response != null)
                    {
                        response.Close();
                    }

                    Logging.Info(String.Format("GetPasswordB Error：", ex.StackTrace));
                }
            }
        }

        internal static void Register(ShadowsocksController controller)
        {
            _controller = controller;
            DoUpdate("初始密码检测");
        }
    }
}
