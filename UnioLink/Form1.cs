using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using WebSocketSharp;
using WebSocketSharp.Server;


// References
// https://so-zou.jp/software/tech/programming/c-sharp/thread/thread-safe-call.htm
// https://ameblo.jp/tubutappuri-san/entry-12598949900.html
// https://qiita.com/ta-yamaoka/items/a7ff1d9651310ade4e76
// https://qiita.com/FumiyaHr/items/13de3dcbd9b81d9d27f0
// https://qiita.com/riyosy/items/5789ccdeee644b34a743
namespace UnioLink
{
    public partial class Form1 : Form
    {
        private WebSocketServer server;
        private int webSocketServerPort = 12345;
        private Timer _timer = null;

        public Form1()
        {
            InitializeComponent();

            server = new WebSocketServer(webSocketServerPort);
            server.AddWebSocketService<ExWebSocketBehavior>("/");
            server.Start();

            StartTimer();
        }

        protected override void OnClosed(EventArgs e)
        {
            StopTimer();

            if (server != null)
                server.Stop();
        }

        private void StartTimer()
        {
            Timer timer = new Timer();
            timer.Tick += new EventHandler(TickHandler);
            timer.Interval = 1000;
            timer.Start();
            _timer = timer;
        }

        private void TickHandler(object sender, EventArgs e)
        {
            string msg = $"Toio count: {ToioBridge.ToioDeviceManager.Instance.GetToioCount()}";
            SetText(msg);
        }

        private void StopTimer()
        {
            if (_timer == null)
            {
                return;
            }
            _timer.Stop();
            _timer = null;
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
                labelToioStatus.Text = text;
        }
    }

    public class ExWebSocketBehavior : WebSocketBehavior
    {
        public static List<ExWebSocketBehavior> clientList = new List<ExWebSocketBehavior>();
        static int globalSeq = 0;
        int seq;

        protected override void OnOpen()
        {
            globalSeq++;
            this.seq = globalSeq;
            clientList.Add(this);
            Debug.WriteLine("Seq" + this.seq + " Login. (" + this.ID + ")");

            string welcomeMessage = "{\"UnioLink\":\"Connected\"}";
            Send(welcomeMessage);

            //foreach (var client in clientList)
            //{
            //    client.Send(welcomeMessage);
            //}
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            string messageString = e.Data;
            Debug.WriteLine("messageString : " + messageString);
            if (messageString.Length > 0 && messageString[0] == '{')
            {
                try
                {
                    UniToio.NetData netData = JsonConvert.DeserializeObject<UniToio.NetData>(messageString);
                    if (netData != null)
                    {
                        if (netData.uuid == "" && (netData.data == null || netData.data.Length == 0)) // Connect
                        {
                            ToioBridge.ToioDeviceManager.Instance.Search(3000, NewToioFound);
                            return;
                        }
                        UniToio.Data data = UniToio.DataConverter.TryConvert(netData);
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
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }
            }
        }

        protected override void OnClose(CloseEventArgs e)
        {
            //Debug.WriteLine("Seq" + this.seq + " Logout. (" + this.ID + ")");
            //clientList.Remove(this);
            //foreach (var client in clientList)
            //{
            //    client.Send("Seq:" + seq + " Logout.");
            //}
        }

        private void NewToioFound(ToioBridge.Toio toio)
        {
            string msg = $"Toio count: {ToioBridge.ToioDeviceManager.Instance.GetToioCount()}";
            //SetText(msg);
            Debug.WriteLine(msg);

            string json = "{\"serial\":" + toio.SerialNumber + "}";
            Sessions.Broadcast(json);

            toio.onValueChanged += OnValueChanged;
        }

        private void OnValueChanged(int serial, string uuid, byte[] data)
        {
            UniToio.Data d = new UniToio.Data();
            d.serial = serial;
            d.uuid = uuid;
            d.data = data;
            UniToio.NetData rd = UniToio.DataConverter.TryConvert(d);
            string json = JsonConvert.SerializeObject(rd);

            Debug.WriteLine("OnValueChanged: " + json);
            Sessions.Broadcast(json);
        }
    }
}


namespace UniToio
{
    public class NetData
    {
        public int serial;
        public string uuid;
        public int[] data;
    }

    public class Data
    {
        public int serial;
        public string uuid;
        public byte[] data;
    }

    public class DataConverter
    {
        public static Data TryConvert(NetData rd)
        {
            if (rd == null || rd.uuid == null || rd.uuid == "" || rd.data == null)
                return null;

            Data data = new Data();
            data.serial = rd.serial;
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

        public static NetData TryConvert(Data rd)
        {
            if (rd == null || rd.uuid == null || rd.uuid == "" || rd.data == null)
                return null;

            NetData data = new NetData();
            data.serial = rd.serial;
            data.uuid = rd.uuid.ToUpper();
            data.data = new int[rd.data.Length];
            for (int i = 0; i < rd.data.Length; i++)
            {
                data.data[i] = rd.data[i];
            }

            return data;
        }
    }
}