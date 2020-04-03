using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;
using System.Web.Script.Serialization;
using System.Windows.Forms.DataVisualization.Charting;
using System.Net.Sockets;
using System.Diagnostics;

namespace NFKLagometer
{
    public partial class Form1 : Form
    {
        private static List<PingItem> pings = new List<PingItem>();
        private Random rnd = new Random();
        StripLine stripline = new StripLine();
        private int Interval = 100;
        private int Timeout = 300;

        private ushort ServerPort;
        private IPAddress ServerIP;

        private long PingCount = 0;
        private long PingSum = 0;

        public Form1()
        {
            InitializeComponent();

        }
        Dictionary<string, int> tags = new Dictionary<string, int>() {
            { "test", 10 },
            { "my", 3 },
            { "code", 8 }
        };

        int lostCount = 0;
        private void Timer1_Tick(object sender, EventArgs e)
        {
            bool lost = false;
            var lat = SendPing(ServerIP, ServerPort, ref lost);

            if (lost)
            {
                lostCount++;
                return;
            }

            var ping = new PingItem(lat);
            //pings.Add(ping);
            PingCount++;
            PingSum += lat;

            var avgPing = PingSum / PingCount;
            stripline.IntervalOffset = avgPing;
            chart1.Series[0].Name = "Ping  " + ping.GetPing();
            chart1.Series[1].Name = "Average  " + (int)avgPing;
            chart1.Series[2].Name = "Count  " + (int)PingCount;
            chart1.Series[3].Name = "Lost  " + (int)lostCount;

            chart1.Series[0].Points.AddXY(ping.GetTime(), ping.Ping);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            timer1.Interval = Interval;

            BtnReload_Click(null, null);

            // add averige line
            stripline.Interval = 0;
            stripline.StripWidth = 1;
            stripline.BackColor = Color.DarkOrange;
            chart1.ChartAreas[0].AxisY.StripLines.Add(stripline);

            // set transpacency
            chart1.Series[0].Color = Color.FromArgb(180, Color.Green);
            chart1.Series[1].Color = stripline.BackColor;
            chart1.Series[2].Color = Color.Transparent; // count
            chart1.Series[3].Color = Color.Transparent; // lost
        }




        private static object DeserializeJson<T>(string Json)
        {
            var JavaScriptSerializer = new JavaScriptSerializer();
            return JavaScriptSerializer.Deserialize<T>(Json);
        }


        bool started = false;

        private void BtnStart_Click(object sender, EventArgs e)
        {
            if (!started)
            {
                // port as int
                ushort.TryParse(txtPort.Text, out ServerPort);
                txtPort.Text = ServerPort.ToString();
                IPAddress.TryParse(txtIp.Text, out ServerIP);
                if (ServerIP == null || ServerPort <= 0)
                {
                    MessageBox.Show("Invalid IP or Port", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                txtIp.Text = ServerIP.ToString();

                btnStart.Text = "Stop";
                btnReload.Enabled =
                    txtIp.Enabled =
                    txtPort.Enabled =
                    cmbServerList.Enabled = false;

                // reset
                pings.Clear();
                chart1.Series[0].Points.Clear();
                PingCount = 0;
                PingSum = 0;

                timer1.Enabled = true;
            }
            else
            {
                btnStart.Text = "Start";
                timer1.Enabled = false;

                btnStart.Enabled =
                    btnReload.Enabled =
                    txtIp.Enabled =
                    txtPort.Enabled =
                    cmbServerList.Enabled = true;
            }

            // toggle
            started = !started;
        }


        byte[] pingPacket = { 0x00, 0x01, 0x05, 0x01, 0x48 };
        Stopwatch sw = new Stopwatch();

        private int SendPing(IPAddress ip, ushort port, ref bool lost)
        {
            try
            {
                using (var client = new UdpClient(port))
                {
                    client.Client.SendTimeout = Timeout;
                    client.Client.ReceiveTimeout = Timeout;
                    var server = new IPEndPoint(ip, port);
                    sw.Restart();
                    client.Send(pingPacket, pingPacket.Length, server);
                    byte[] packet = client.Receive(ref server);
                    sw.Stop();
                }
            }
            catch (Exception e)
            {
                lost = true;
            }
            return (int)sw.ElapsedMilliseconds;
        }

        private void BtnReload_Click(object sender, EventArgs e)
        {
            btnReload.Enabled = false;

            var url = "https://stats.needforkill.ru/api.php?action=gsl";
            using (var wc = new WebClient())
            {
                try
                {
                    var data = wc.DownloadString(url);
                    var servers = DeserializeJson<List<ServerItem>>(data) as List<ServerItem>;
                    cmbServerList.Items.Clear();
                    foreach (var s in servers)
                    {
                        cmbServerList.Items.Add(s);
                    }
                    cmbServerList.SelectedIndex = 0;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error load servers from\n" + url, "API Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            btnReload.Enabled = true;
        }

        private void CmbServerList_SelectedIndexChanged(object sender, EventArgs e)
        {
            var item = (ServerItem)cmbServerList.SelectedItem;
            var address = item.Ip.Split(':');
            txtIp.Text = address[0];
            txtPort.Text = address[1];
        }

        private void Chart1_Click(object sender, EventArgs e)
        {

        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            if (this.Width < 500)
                this.Width = 500;
            if (this.Height < 400)
                this.Height = 400;
        }
    }

    internal class ServerItem
    {

        public string Name;
        public string Hostname;
        public string Map;
        public string Gametype;
        public string Load;
        public string Ip;
        //public string[] Players;
        public override string ToString()
        {
            return Name;
        }
    }


    internal class PingItem
    {
        public PingItem(int ping)
        {
            Ping = ping;
        }

        public DateTime Date = DateTime.Now;
        public int Ping = 0;

        public string GetTime()
        {
            return Date.Hour + ":" + Date.Minute + ":" + Date.Second;
        }

        public string GetPing()
        {
            return Ping.ToString().PadLeft(3, ' ');
        }
    }
}
