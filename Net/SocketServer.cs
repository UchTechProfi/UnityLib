using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Sockets;
using System.Net;

namespace SharpNet
{
    class SocketServer : SocketBase
    {
        private const int NumberOfListeners = 10000;

        private Socket Server;
        private List<Socket> Sockets = new List<Socket>();
        /// <summary>
        /// Число подключенных клиентов.
        /// </summary>
        public int NumberOfClients = 0;
        public delegate void OnAcceptHandler(CallbackedSocket Client);
        /// <summary>
        /// Событие, возникающее при подключении клиента. 
        /// </summary>
        public event OnAcceptHandler OnAccept; //Не получится ли подключение раньше, чем подписка? Подумать о переделке так же, как и в клиенте. 

        /// <summary>
        /// Создаёт сервер с локальной конечной точкой по умолчанию (127.0.0.1, порт 10000). 
        /// </summary>
        public SocketServer()
        {
            Server = new Socket(AddressFamily.InterNetwork ,SocketType.Stream, ProtocolType.Tcp);
            EndPoint endPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 10000); //Временное решение, нужно реализовать подключение к заданному порту и хосту. Done, см. перегрузку.
            try
            {
                Server.Bind(endPoint);
                Server.Listen(NumberOfListeners);
                for (int i = 0; i <= 4; i++ ) Server.BeginAccept(AcceptCallback, Server);
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex.ToString());
                Console.WriteLine(ex.ErrorCode);
            }
        }

        /// <summary>
        /// Создаёт сервер с указанной локальной конечной точкой. 
        /// </summary>
        /// <param name="host">IP-адрес хоста.</param>
        /// <param name="port">Локальный порт.</param>
        public SocketServer(string host, int port)
        {
            Server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            EndPoint endPoint = new IPEndPoint(IPAddress.Parse(host), port); 
            try
            {
                Server.Bind(endPoint);
                Server.Listen(NumberOfListeners);
                for (int i = 0; i <= 4; i++) Server.BeginAccept(AcceptCallback, Server);
            }
            catch (SocketException ex)
            {
                Console.WriteLine(ex.ToString());
                Console.WriteLine(ex.ErrorCode);
            }
        }

        private void AcceptCallback(IAsyncResult ar)
        {
            Interlocked.Increment(ref NumberOfClients);
            Socket Listener = (Socket)ar.AsyncState;
            Socket LocalClient = Listener.EndAccept(ar);
            CallbackedSocket Client = new CallbackedSocket(LocalClient);
            if (OnAccept != null) OnAccept(Client);
            lock (Sockets)
            {
                Sockets.Add(LocalClient);
            }
            Listener.BeginAccept(AcceptCallback, Listener);
            SocketState State = new SocketState();
            State.Client = LocalClient;
            try
            {
                State.Client.BeginReceive(State.Buffer, 0, State.Buffer.Length, SocketFlags.None, ReceiveCallback, State);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        /// <summary>
        /// Выполняет широковещательную рассылку для всех подключенных клиентов. 
        /// </summary>
        /// <param name="Data">Массив байтов для отправки.</param>
        public void SendToAll(byte[] Data)
        {
            Send(Data);
        }

        /*public void SendTo(int number, byte[] Data)
        {
            Sockets[number].BeginSend(Data, 0, Data.Length, SocketFlags.None, SendCallback, Sockets[number]);
        }*/

        protected sealed override void DoSend(byte[] Data)
        {
            foreach (Socket socket in Sockets)
            {
                if (socket.Connected) socket.BeginSend(Data, 0, Data.Length, SocketFlags.None, SendCallback, socket);
            }
        }
    }
}
