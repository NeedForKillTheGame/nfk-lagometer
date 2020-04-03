using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace NFKLagometer
{
    public class TimeoutWebClient : WebClient
    {
        /// <summary>
        /// Seconds
        /// </summary>
        public int Timeout = 1;

        protected override WebRequest GetWebRequest(Uri uri)
        {
            WebRequest w = base.GetWebRequest(uri);
            w.Timeout = Timeout * 1000;
            return w;
        }
    }
}
