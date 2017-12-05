using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shadowsocks.Extension;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace test
{
    [TestClass]
    public class AutoPassTest
    {
        [TestMethod]
        public void GetPasswordBTest()
        {
            var bb = new List<(String Address, String Password, Int32 Port, String Method)>();
            AutoPassword.GetPasswordB(bb);
            Console.WriteLine(bb);
        }


        [TestMethod]
        public void GetPasswordCTest()
        {
            var bb = new List<(String Address, String Password, Int32 Port, String Method)>();
            AutoPassword.GetPasswordC(bb);
            Console.WriteLine(bb);
        }
    }
}
