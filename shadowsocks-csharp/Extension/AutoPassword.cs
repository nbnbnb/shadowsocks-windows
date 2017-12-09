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
    public static class AutoPassword
    {
        private static Timer _timer;
        private static ShadowsocksController _controller;
        private static List<String> hosts;
        static AutoPassword()
        {
            hosts = File.ReadAllLines("hosts.txt").ToList();
            // 创建计时器但不启动
            // 确保 _timer 在线程池调用 PasswordCheck 之前引用该计时器
            _timer = new Timer(PasswordCheck, null, Timeout.Infinite, Timeout.Infinite);
            // 现在 _timer 已被赋值，可以启动计时器了
            // 现在在 PasswordCheck 中调用 _timer 保证不会抛出 NullReferenceException
            _timer.Change(0, Timeout.Infinite);
            Logging.Info("----------------------------------------开启 ishadowsocks 监听");
        }

        static void DoUpdate(string msg)
        {
            Logging.Info("----------------------------------------" + msg);
            Task.Run(() => UpdateConfig()); // 异步查询
        }

        static void PasswordCheck(object obj)
        {
            if (DateTime.Now.Minute == 0
                || DateTime.Now.Minute == 57
                || DateTime.Now.Minute == 58
                || DateTime.Now.Minute == 59
                || DateTime.Now.Minute == 1
                || DateTime.Now.Minute == 2
                || DateTime.Now.Minute == 3)
            {
                DoUpdate("整点更新密码");
            }
            _timer.Change(1000 * 40, Timeout.Infinite);  // 30s 检查一次，当为整点时，去读取服务器端更新的密码
        }

        static void UpdateConfig()
        {
            var current_config = Configuration.Load();  // 当前的配置信息
            var fork_config = Configuration.Load("fork-gui-config.json");  // 完整的配置信息
            var addition_config = Configuration.Load("addition-config.json");  // 自定义的不变的配置信息
            var passwords = GetPassword();   // 爬取的配置信息
            bool shouldUpdate = false;
            var spider_servers = new List<Server>();
            foreach (var serverInfo in fork_config.configs)
            {
                // 通过 Server 进行匹配
                var (Address, Password, Port, Method) = passwords.FirstOrDefault(m => m.Address.Equals(serverInfo.server, StringComparison.OrdinalIgnoreCase));
                if (Address != null)
                {
                    if (!Password.Equals(serverInfo.password, StringComparison.Ordinal) ||
                        Port != serverInfo.server_port ||
                        !Method.Equals(serverInfo.method, StringComparison.Ordinal))
                    {
                        shouldUpdate = true;
                        serverInfo.password = Password;
                        serverInfo.server_port = Port;
                        serverInfo.method = Method;
                    }

                    // 只有匹配成功的才认为是爬取成功的
                    spider_servers.Add(serverInfo);
                }
            }

            // 如果有地址未获取成功，则也应该更新
            if (!shouldUpdate && passwords.Count != current_config.configs.Count - addition_config.configs.Count)
            {
                shouldUpdate = true;
            }

            if (shouldUpdate)
            {
                spider_servers.AddRange(addition_config.configs);  // 加入自定义的配置信息
                current_config.configs = spider_servers;  // 赋值后更新
                _controller.Stop();
                Configuration.Save(current_config);
                Logging.Info("----------------------------------------密码改变，更新成功");
                // 将会重新载入配置文件
                // 第一次时，需要等待一下
                Thread.Sleep(1000);
                _controller.Start();
            }
            else
            {
                Logging.Info("----------------------------------------密码未变，无需更新");
            }
        }

        static List<(String Address, String Password, Int32 Port, String Method)> GetPassword()
        {
            var res = new List<(String Address, String Password, Int32 Port, String Method)>();
            //GetPasswordA(res);
            GetPasswordB(res);
            GetPasswordC(res);
            return res;
        }

        #region 无效地址

        /*
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
        */

        #endregion

        /// <summary>
        /// 从 https://en.ss8.fun/ 获取图片资源
        /// </summary>
        /// <param name="res"></param>
        public static void GetPasswordB(List<(String Address, String Password, Int32 Port, String Method)> res)
        {
            string host = hosts[0];
            if (String.IsNullOrWhiteSpace(host))
            {
                return;
            }

            string[] images = { "server01.png", "server02.png", "server03.png" };

            foreach (string image in images)
            {
                WebRequest request = WebRequest.Create(String.Format("{0}/images/{1}?timestamp={2}", host, image, DateTime.Now.Ticks));
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
                                var sv = Server.ParseLegacyURL(result.Text); // ssURL
                                res.Add((sv.server, sv.password, sv.server_port, sv.method));
                                Logging.Info(String.Format("----------------------------------------获取帐号：{0}:{1}-{2}-{3}", sv.server, sv.server_port, sv.password, sv.method));
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

        /// <summary>
        /// 从 https://go.ishadowx.net/ 获取密码
        /// 0/6/12/24 GMT+8
        /// 备用地址 isx.yt isx.tn
        /// 编辑任意邮件发送至下面的邮箱，将会自动回复最新地址
        /// Email: admin@ishadowshocks.com
        /// </summary>
        /// <param name="res"></param>
        public static void GetPasswordC(List<(String Address, String Password, Int32 Port, String Method)> res)
        {
            string host = hosts[1];
            if (String.IsNullOrWhiteSpace(host))
            {
                return;
            }

            Regex ip_reg = new Regex(@"<h4>(IP Address|IP地址):<span id=""ip(us|jp|sg)[abc]"">(?<IP>.+?)</span>");
            Regex password_reg = new Regex(@"<h4>(Password|密码):<span id=""pw(us|jp|sg)[abc]"">(?<Password>\d+)");
            Regex port_reg = new Regex(@"<h4>(Port|端口):<span id=""port(us|jp|sg)[abc]"">(?<Port>\d+)");
            Regex method_reg = new Regex(@"<h4>Method:(?<Method>.+?)</h4>");

            WebRequest request = WebRequest.Create(String.Format("{0}/?timestamp={1}", host, DateTime.Now.Ticks));
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
                            var ips = ip_reg.Matches(tp);
                            var passwords = password_reg.Matches(tp);
                            var ports = port_reg.Matches(tp);
                            var methods = method_reg.Matches(tp);

                            if (ips.Count == passwords.Count && ports.Count == ips.Count && ips.Count > 0)
                            {
                                for (int i = 0; i < ips.Count; i++)
                                {
                                    string ip = "";
                                    string password = "";
                                    string method = "";
                                    int port = 0;
                                    ip = ips[i].Groups["IP"].Value;
                                    password = passwords[i].Groups["Password"].Value;
                                    port = Int32.Parse(ports[i].Groups["Port"].Value);
                                    method = methods[i].Groups["Method"].Value;
                                    Logging.Info(String.Format("----------------------------------------获取帐号：{0}:{1}-{2}-{3}", ip, port, password, method));
                                    res.Add((ip, password, port, method));
                                }
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
                Logging.Info(String.Format("----------------------------------------GetPasswordC Error：{0}", ex.StackTrace));
            }
        }

        internal static void Register(ShadowsocksController controller)
        {
            _controller = controller;
            DoUpdate("初始密码检测");
        }
    }
}
