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

    class SocketClient:SocketBase
    {
        private Socket Client;
        private object LockObj = new object();
        private int Port;
        private string Host;
        private int ReconnectDelay = 1000;
        private int ReconnectCount = 3;
        private int CurReconnectNumber = 0;
        public delegate void OnConnectHandler();
        /// <summary>
        /// Событие, возникающее при подключении. 
        /// </summary>
        public event OnConnectHandler OnConnect //Небольшой костыль для нормальной реализации OnConnect (до этого подписка на OnConnect могла произвестись позже подключения, в итоге вызова OnConnect не происходило). 
        {
            add
            {
                lock (LockObj)
                {
                    OnConnectAdded += value;
                    ConnectEvent.Set();
                }
            }
            remove
            {
                lock (LockObj)
                {
                    OnConnectAdded -= value;
                }
            }
        }
        private event OnConnectHandler OnConnectAdded;
        private Thread ConnectThread;
        private ManualResetEvent ConnectEvent = new ManualResetEvent(false);

        /*
         * Есть три функции со схожими названиями: Connect, ConnectCallback, ConnectFunction, разница между ними заключается в следующем. Connect - метод, создающий удалённую конечную точку и выполняющий
         * попытку подключения к ней. Используется в конструкторах и при реконнекте. ConnectCallback - функция обратного вызова, вызывается при успешном завершении подключении, используется в качестве 
         * аргумента в BeginConnect. ConnectFunction - функция, выполняемая в выделенном потоке и нужная для вызова обработчика OnConnect. 
         */

        /// <summary>
        /// Создаёт клиент и подключает его к удалённой конечной точке по умолчанию (127.0.0.1, порт 10000).
        /// </summary>
        public SocketClient()
        {
            Port = 10000;
            Host = "127.0.0.1";
            Client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Connect(Host, Port);
        }

        /// <summary>
        /// Создаёт клиент и подключает его к заданной удалённой конечной точке.
        /// </summary>
        /// <param name="aHost">IP-адрес хоста.</param>
        /// <param name="aPort">Номер порта.</param>
        /// <returns></returns>
        public SocketClient(string aHost, int aPort)
        {
            Host = aHost;
            Port = aPort;
            Client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Connect(aHost, aPort);
        }

        /// <summary>
        /// Создаёт клиент, подлкючает его к заданной удалённой конечной точке и устанавливает количество переподключений и задержку переподключения.
        /// </summary>
        /// <param name="aHost">IP-адрес хоста.</param>
        /// <param name="aPort">Номер порта.</param>
        /// <param name="aReconnectDelay">Задержка переподключения, мс.</param>
        /// <param name="aReconnectCount">Количество переподключений.</param>
        public SocketClient(string aHost, int aPort, int aReconnectDelay, int aReconnectCount)
        {
            Host = aHost;
            Port = aPort;
            if (aReconnectDelay < 0) ReconnectDelay = 0;  else ReconnectDelay = aReconnectDelay;
            if (aReconnectCount < 0) ReconnectCount = 0; else ReconnectCount = aReconnectCount;
            Client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            Connect(aHost, aPort);
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            Socket socket = (Socket)ar.AsyncState;
            try
            {
                socket.EndConnect(ar);
                ConnectThread.Start();
                SocketState State = new SocketState();
                State.Client = socket;
                socket.BeginReceive(State.Buffer, 0, State.Buffer.Length, SocketFlags.None, ReceiveCallback, State);
                CurReconnectNumber = 0; //Если началась отправка, то подключение наверняка установлено, счётчик реконнектов можно сбрасывать. 
            }
            catch (SocketException ex) //Реконнект не работает, переделать. Done, теперь работает. 
            {
                Console.WriteLine(ex.ToString());
                Thread.Sleep(ReconnectDelay);
                Interlocked.Increment(ref CurReconnectNumber); //Несколько избыточно использовать блокированное инкрементирование, но на всякий случай пусть так. 
                                                               //Если в будущих версиях потребуется коннект к нескольким серверам, этот лок спасёт пару метров нервых клеток. 
                if (CurReconnectNumber < ReconnectCount) Connect(Host, Port);
            }
        }

        private void Connect(string aHost, int aPort)
        {
            EndPoint RemotePoint = new IPEndPoint(IPAddress.Parse(aHost), aPort);
            if (ConnectThread == null) ConnectThread = new Thread(new ThreadStart(ConnectFunction));
            ConnectThread.IsBackground = true;
            if (!Client.Connected) Client.BeginConnect(RemotePoint, ConnectCallback, Client);
        }

        protected sealed override void DoSend(byte[] Data)
        {
            if (Client != null) Client.BeginSend(Data, 0, Data.Length, SocketFlags.None, SendCallback, Client);
        }

        private void ConnectFunction()
        {
            ConnectEvent.WaitOne();
            OnConnectAdded();
        }
    }
}
