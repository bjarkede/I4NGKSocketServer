using System;
using System.Net.Security;
using System.Threading;

namespace SocketServNGK
{
    class Program
    {
        private static AsyncSocket server = new AsyncSocket();

        static void Main(string[] args)
        {
            server.ListenBegin();
        }
    }
}
