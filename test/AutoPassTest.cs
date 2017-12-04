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
        public void GetPasswordCTest()
        {
            List<Tuple<String, String, Int32>> bb = new List<Tuple<String, String, Int32>>();

            AutoPassword.GetPasswordC(bb);

            Console.WriteLine(bb);
        }
    }
}
