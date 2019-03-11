using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Threading;
using Shadowsocks.Controller;
using Shadowsocks.Model;

namespace Shadowsocks.Util
{
    class SwitchPing
    {
        private static Ping ping = new Ping();
        private static int checkCount = 5;
        private static long failCount = 0;
        private static long successCount = 0;  // 0 表示为 TCloud，1 表示为 WootHosting
        private static ShadowsocksController shadowsocksController;

        public static async void Start(ShadowsocksController controller)
        {
            shadowsocksController = controller;
            ping.PingCompleted += Ping_PingCompleted;
            while (true)
            {
                await Task.Delay(1000);
                if (shadowsocksController.GetCurrentConfiguration().autoPing)
                {
                    try
                    {
                        ping.SendAsync("155.94.182.154", 1000, null);  // 设置最大响应时间 1000ms
                    }
                    catch (PingException ex)
                    {
                        Logging.Info($"PingException {ex.Message}");

                    }
                    catch (Exception ex)
                    {
                        Logging.LogUsefulException(ex);
                    }
                }
            }
        }

        private static void Ping_PingCompleted(object sender, PingCompletedEventArgs e)
        {
            if (e.Reply != null)
            {
                if (e.Reply.Status != IPStatus.Success)
                {
                    failCount++;
                    if (failCount >= checkCount)
                    {
                        successCount = 0;
                        if (!IsTClouding)
                        {
                            Logging.Info("Switch to TCloud");
                            shadowsocksController.SelectServerIndex(0);
                        }

                    }
                }
                else
                {
                    successCount++;
                    if (successCount >= checkCount)
                    {
                        failCount = 0;
                        if (IsTClouding)
                        {
                            Logging.Info("Switch to WootHosting");
                            // 切换到 WootHosting
                            shadowsocksController.SelectServerIndex(1);
                        }
                    }
                }
                Logging.Info($"Time: {e.Reply.RoundtripTime} FailCount: {failCount} SuccessCount: {successCount}");
            }
            else
            {
                Logging.Info("Relpy is null");
            }
        }

        private static bool IsTClouding
        {
            get
            {
                return shadowsocksController.GetCurrentConfiguration().index == 0;
            }
        }
    }
}
