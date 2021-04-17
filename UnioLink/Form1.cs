using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

// References
// https://so-zou.jp/software/tech/programming/c-sharp/thread/thread-safe-call.htm
// https://ameblo.jp/tubutappuri-san/entry-12598949900.html
// https://qiita.com/ta-yamaoka/items/a7ff1d9651310ade4e76
// https://qiita.com/FumiyaHr/items/13de3dcbd9b81d9d27f0
namespace UnioLink
{
    public partial class Form1 : Form
    {
        private MessageWebSocket messageWebSocket;

        private static string WebSocketUri = "ws://127.0.0.1:12345";

        public Form1()
        {
            InitializeComponent();
            Connect();
        }

        delegate void SetTextCallback(string text);

        private void SetText(string text)
        {
            if (!labelToioStatus.IsHandleCreated || labelToioStatus.IsDisposed)
                return;

            if (labelToioStatus.InvokeRequired)
            {
                SetTextCallback delegateMethod = new SetTextCallback(SetText);
                labelToioStatus.Invoke(delegateMethod, new object[] { text });
            }
            else
            {
                labelToioStatus.Text = text;
            }
        }

        private void NewToioFound()
        {
            string msg = $"Toio count: {ToioBridge.ToioDeviceManager.Instance.GetToioCount()}";
            SetText(msg);
            
        }

        private void buttonConnect_Click(object sender, EventArgs e)
        {
            ToioBridge.ToioDeviceManager.Instance.Search(3000, NewToioFound);
        }

        private void Connect()
        {
            Debug.WriteLine("OnConnect()");

            messageWebSocket = new MessageWebSocket();

            //In this case we will be sending/receiving a string so we need to set the MessageType to Utf8.
            messageWebSocket.Control.MessageType = SocketMessageType.Utf8;

            //Add the MessageReceived event handler.
            messageWebSocket.MessageReceived += MessageWebSocket_MessageReceived;

            //Add the Closed event handler.
            messageWebSocket.Closed += MessageWebSocket_Closed;

            Uri serverUri = new Uri(WebSocketUri);

            try
            {
                Task.Run(async () => {
                    //Connect to the server.
                    Debug.WriteLine("Connect to the server...." + serverUri.ToString());
                    await Task.Run(async () =>
                    {
                        await messageWebSocket.ConnectAsync(serverUri);
                        Debug.WriteLine("ConnectAsync OK");

                        //await WebSock_SendMessage(messageWebSocket, "Connect Start");

                        //= JsonSerializer.Serialize("");
                        await WebSock_SendMessage(messageWebSocket, "C#");
                    });

                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("error : " + ex.ToString());

                //Add code here to handle any exceptions
            }

        }

        private void MessageWebSocket_Closed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            Debug.WriteLine("MessageWebSocket_Closed()");

            // TODO
            // try reconnect
        }

        private void MessageWebSocket_MessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            DataReader messageReader = args.GetDataReader();
            messageReader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
            string messageString = messageReader.ReadString(messageReader.UnconsumedBufferLength);
            Debug.WriteLine("messageString : " + messageString);

            if (messageString.Length > 0 && messageString[0] == '{')
            {
                try
                {
                    UniToio.RawData rawData = JsonConvert.DeserializeObject<UniToio.RawData>(messageString);
                    if (rawData != null)
                    {
                        UniToio.Data data = UniToio.DataConverter.TryConvert(rawData);
                        if (data != null)
                        {
                            for (int i = 0; i < ToioBridge.ToioDeviceManager.Instance.GetToioCount(); i++)
                            {
                                ToioBridge.Toio toio = ToioBridge.ToioDeviceManager.Instance.GetToio(i);
                                toio.Write(data.uuid, data.data);
                            }
                            Debug.WriteLine(data.uuid);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.ToString());
                }

            }

            //Task.Run(async () =>
            //{
            //    await Task.Delay(100);
            //});
        }

        private async Task WebSock_SendMessage(MessageWebSocket webSock, string message)
        {
            Debug.WriteLine("WebSock_SendMessage : " + message);

            DataWriter messageWriter = new DataWriter(webSock.OutputStream);
            messageWriter.WriteString(message);
            await messageWriter.StoreAsync();
        }
    }
}


namespace UniToio
{
    [Serializable]
    public class RawData
    {
        public string uuid;
        public int[] data;
    }

    public class Data
    {
        public string uuid;
        public byte[] data;
    }

    public class DataConverter
    {
        public static Data TryConvert(RawData rd)
        {
            if (rd == null || rd.uuid == null || rd.uuid == "" || rd.data == null)
                return null;

            Data data = new Data();
            data.uuid = rd.uuid.ToLower();
            data.data = new byte[rd.data.Length];
            for (int i = 0; i < rd.data.Length; i++)
            {
                if (rd.data[i] < 0)
                    rd.data[i] = 0;
                else if (rd.data[i] > 255)
                    rd.data[i] = 255;

                data.data[i] = (byte)rd.data[i];
            }

            return data;
        }
    }

    public class motorControlData
    {
        public static readonly string Uuid = "10B20102-5B3B-4571-9508-CF3EFCD7BBAE";
        public static readonly int DataLength = 7;

        public static readonly byte Type = 0x01;
        public static readonly byte LeftId = 0x01;
        public static readonly byte RightId = 0x02;

        public string uuid = Uuid;
        public byte[] data;

        public motorControlData() { }

        public motorControlData(bool isLeftForward, byte leftSpeed, bool isRightForward, byte rightSpeed)
        {
            uuid = Uuid;
            data = new byte[7] {Type,
                LeftId, (byte)(isLeftForward ? 0x01 : 0x02), leftSpeed,
                RightId, (byte)(isRightForward ? 0x01 : 0x02), rightSpeed};
        }
    }
}