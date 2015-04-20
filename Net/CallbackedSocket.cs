using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace SharpNet
{
    class CallbackedSocket : SocketBase
    {
        private Socket socket;

        public CallbackedSocket(Socket Client)
        {
            socket = Client;
        }

        protected override void DoSend(byte[] Data)
        {
            if (socket != null) socket.BeginSend(Data, 0, Data.Length, SocketFlags.None, SendCallback, socket);
        }
    }
}
