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
    abstract class SocketBase
    {
        protected const int ReceiveBufferSize = 4;

        public delegate void OnDataHandler(CallbackedSocket Client, byte[] Data);
        /// <summary>
        /// Событие, возникающее при приёме данных. 
        /// </summary>
        public event OnDataHandler OnData;
        //private Socket SocketToSend; //Костыль a little bit. 
        //private bool IsFromReceive; //Ещё один, исправить по уму. 
        private object obj = new object();

        /*
         * Проблема с костылями состоит в следующем. Отправка сообщения может быть реализована двумя путями: из обработчика OnData и из клиента и сервера напрямую. Если отправка выполняется из обработчика 
         * OnData, то используется сокет SocketToSend (см. первый костыль). В любом случае, откуда бы ни была отправка, используется метод Send этого класса. Если отправка из обработчика, выставляется флаг 
         * IsFromReceive, который поверяется в Send, и используется SocketToSend. Если отправка напрямую из сервера/клиента, то флаг не выставляется и выполняется абстрактный метод DoSend (читай - сервер/клиент 
         * сам решает, какой сокет использовать для отправки). Проблема возникает в случае, когда одновременно используется отправка из обработчика и напрямую. Примерный сценарий этого может быть таков:
         * по получении определённого сообщения от какого-либо клиента сервер отправляет широковещательную отправку всем клиентам:
         * 
         * Server.OnData += delegate(SocketBase sb, byte[] Data)
            {
                Server.SendToAll(Data); 
            };
         * 
         * В случае правильной реализации SendToAll вызывает Send, который вызывает DoSend, переопределённый в ServerSocket, а DoSend, в свою очередь, выполняет нужные действия. В текущей реализации SendToAll
         * вызовет Send, внутри Send произойдёт проверка на IsFromReceive, а поскольку вызов Send произошёл из обработчика OnData и флаг IsFromReceive выставлен, то выполнится отправка от сокета SocketToSend,
         * а перегруженный DoSend не исполнится. В итоге сообщение будет отправлено не широковещательно, а на сокет, находящийся в SocketState.Client, т. е. на клиента, отправившего сообщение. 
         * P. S. Костыли убраны, код поправлен. 
         * */

        protected void ReceiveCallback(IAsyncResult ar)
        {
            SocketState State = (SocketState)ar.AsyncState;
            Socket LocalClient = State.Client;
            int BytesRead = LocalClient.EndReceive(ar);
            if (BytesRead > 0)
            {
                if (State.IsStart)
                {
                    int Size = BitConverter.ToInt32(State.Buffer, 0);
                    State.Buffer = new byte[Size];
                    State.IsStart = false;
                    State.Client.BeginReceive(State.Buffer, 0, State.Buffer.Length, SocketFlags.None, ReceiveCallback, State);
                }
                else
                {
                    State.IsStart = true;
                    CallbackedSocket socket = new CallbackedSocket(State.Client);
                    OnData(socket, State.Buffer);
                    State.Buffer = new byte[ReceiveBufferSize];
                    State.Client.BeginReceive(State.Buffer, 0, State.Buffer.Length, SocketFlags.None, ReceiveCallback, State);
                }
            }
        }

        /// <summary>
        /// Отправляет массива байтов. Если отправка ведётся из клиента, то сообщение будет доставлено
        /// на сервер, если из сервера - на всех клиентов.
        /// </summary>
        /// <param name="Data">Массив байтов для отправки.</param>
        public void Send(byte[] Data) //Реализовать отправку с длиной. Done. 
        {
            byte[] InsideData = new byte[Data.Length + ReceiveBufferSize];
            byte[] Size = new byte[ReceiveBufferSize];
            Size = BitConverter.GetBytes(Data.Length);
            Buffer.BlockCopy(Size, 0, InsideData, 0, Size.Length);
            Buffer.BlockCopy(Data, 0, InsideData, Size.Length, Data.Length);
            DoSend(InsideData);
        }

        abstract protected void DoSend(byte[] Data);


        protected void SendCallback(IAsyncResult ar)
        {
            Socket socket = (Socket)ar.AsyncState;
            socket.EndSend(ar);
        }

        protected class SocketState
        {
            public Socket Client = null;
            public byte[] Buffer;
            public bool IsStart = true;

            public SocketState()
            {
                Buffer = new byte[ReceiveBufferSize];
            }
        }
    }
}
