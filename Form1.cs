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
using System.Reflection;

namespace NFKLagometer
{
    public partial class Form1 : Form
    {
        private static List<PingItem> pings = new List<PingItem>();
        private Random rnd = new Random();
        StripLine stripline = new StripLine();

        private ushort ServerPort;
        private IPAddress ServerIP;

        private long PingCount = 0;
        private long PingSum = 0;
        private long PingMin = 999;
        private long PingMax = 0;
        private int LostCount = 0;

        byte[] pingPacket = { 0x00, 0x01, 0x05, 0x01, 0x48 };

        public Form1()
        {
            InitializeComponent();

        }
        Dictionary<string, int> tags = new Dictionary<string, int>() {
            { "test", 10 },
            { "my", 3 },
            { "code", 8 }
        };

        private void Timer1_Tick(object sender, EventArgs e)
        {
            bool lost = false;
            var lat = SendPing(ServerIP, ServerPort, ref lost);

            if (lost)
            {
                LostCount++;
                return;
            }

            var ping = new PingItem(lat);
            //pings.Add(ping);
            PingCount++;
            PingSum += lat;

            if (PingMin == 0 || PingMin > lat)
                PingMin = lat;
            if (lat > PingMax)
                PingMax = lat;

            var avgPing = PingSum / PingCount;
            stripline.IntervalOffset = avgPing;
            chart1.Series[0].Name = "Ping  " + ping.GetPing();
            chart1.Series[1].Name = "Average  " + (int)avgPing;
            chart1.Series[2].Name = "Min  " + (int)PingMin;
            chart1.Series[3].Name = "Max  " + (int)PingMax;
            chart1.Series[4].Name = "Count  " + (int)PingCount;
            chart1.Series[5].Name = "Lost  " + (int)LostCount;

            chart1.Series[0].Points.AddXY(ping.GetTime(), ping.Ping);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // set version
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            this.Text = this.Text + " v" + ver.Major + "." + ver.Minor;


            BtnReload_Click(null, null);

            // add averige line
            stripline.Interval = 0;
            stripline.StripWidth = 1;
            stripline.BackColor = Color.DarkOrange;
            chart1.ChartAreas[0].AxisY.StripLines.Add(stripline);

            // set transpacency
            chart1.Series[0].Color = Color.FromArgb(180, Color.Green); // ping
            chart1.Series[1].Color = stripline.BackColor; // avg
            chart1.Series[2].Color = Color.Transparent; // min
            chart1.Series[3].Color = Color.Transparent; // max
            chart1.Series[4].Color = Color.Transparent; // count
            chart1.Series[5].Color = Color.Transparent; // lost
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
                    numInterval.Enabled =
                    numTimeout.Enabled =
                    cmbServerList.Enabled = false;

                timer1.Interval = (int)numInterval.Value;

                // reset
                pings.Clear();
                chart1.Series[0].Points.Clear();
                PingCount = 0;
                PingSum = 0;
                PingMin = 999;
                PingMax = 0;
                LostCount = 0;

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
                    numInterval.Enabled =
                    numTimeout.Enabled =
                    cmbServerList.Enabled = true;
            }

            // toggle
            started = !started;
        }


        Stopwatch sw = new Stopwatch();

        private int SendPing(IPAddress ip, ushort port, ref bool lost)
        {
            try
            {
                using (var client = new UdpClient(port))
                {
                    client.Client.SendTimeout = (int)numTimeout.Value;
                    client.Client.ReceiveTimeout = (int)numTimeout.Value;
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
            using (var wc = new TimeoutWebClient())
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
                    MessageBox.Show("Error load servers from\n" + url + "\n\n" + ex.Message, "API Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
