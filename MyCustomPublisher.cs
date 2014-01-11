
/*
    MyCustomPublisher.cs - Custom Publisher created for an tutorial about
        creating custom publishers
        
        -brad.antoniewicz@foundstone.com

    I dont know what all the legal crap below says, but if you ask me, you can give this
    to anyone you want..  :)
*/



//
// Copyright (c) Michael Eddington
//
// Permission is hereby granted, free of charge, to any person obtaining a copy 
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights 
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
// copies of the Software, and to permit persons to whom the Software is 
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in	
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
//


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Net;
using NLog;
using Peach.Core.IO;

namespace Peach.Core.Publishers
{
	[Publisher("MyCustomPublisher", true)]
	[Parameter("Host", typeof(string), "Hostname or IP address of remote host")]
	[Parameter("Port", typeof(ushort), "Destination port number", "0")]
	[Parameter("Timeout", typeof(int), "How many milliseconds to wait for data/connection (default 3000)", "3000")]
	[Parameter("Interface", typeof(IPAddress), "IP of interface to bind to", "")]
	[Parameter("SrcPort", typeof(ushort), "Source port number", "0")]
	[Parameter("MinMTU", typeof(uint), "Minimum allowable MTU property value", DefaultMinMTU)]
	[Parameter("MaxMTU", typeof(uint), "Maximum allowable MTU property value", DefaultMaxMTU)]
	public class MyCustomPublisher: SocketPublisher
	{
		private static NLog.Logger logger = LogManager.GetCurrentClassLogger();
		protected override NLog.Logger Logger { get { return logger; } }
		private IPEndPoint _remote;

		public MyCustomPublisher(Dictionary<string, Variant> args)
			: base("Udp", args)
		{
		}

		protected override bool AddressFamilySupported(AddressFamily af)
		{
			return (af == AddressFamily.InterNetwork) || (af == AddressFamily.InterNetworkV6);
		}

		protected override Socket OpenSocket(EndPoint remote)
		{
			_remote = (IPEndPoint)remote;
			Socket s = new Socket(remote.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
			return s;
		}

		protected override void FilterOutput(byte[] buffer, int offset, int count)
		{
			base.FilterOutput(buffer, offset, count);

			if (_remote.Port == 0)
				throw new PeachException("Error sending a Udp packet to " + _remote.Address + ", the port was not specified.");
		}
        protected override void OnOutput(BitwiseStream data)
        {

            /* 
                An Abc Proto Packet is structured:
    
                +-------+-------+-------+-------
                [      Hdr     ][    Length    ]
                [             Data          ]...
                
                Hdr = 2 btyes, Length = 2 bytes (network byte order)
                Data is variable length
            */

            if (data.Length > 65535 - 4 || data.Length <= 0)
                throw new PeachException("Abc Proto data length exceeds field length!");

            // Calculate the Length of the Entire Encapsulated Packet (Data + Abc Proto Hdr Len [2 bytes] + Abc Proto Len [2 bytes]
            int totalPktLen = (int)data.Length + 4;

            // Abc Proto Header - 1234 indicates the start of an abcproto packet
            byte[] abcProtoHdr = { 0x12, 0x34 } ;

            // Create a new buffer that will the final encapsulated packet
            var buffer = new byte[totalPktLen];
            
            // Copy Abc Proto Header into our new buffer
            Array.Copy(abcProtoHdr, 0, buffer, 0, abcProtoHdr.Length);

            // Copy AbcProto Length into buffer after Abc Proto Hdr - We're also doing a bit of a int to short conversion here
            Array.Copy(BitConverter.GetBytes(totalPktLen - 4), 0, buffer, abcProtoHdr.Length, sizeof(ushort));
            // Rearrange the Length to be in Big Endian (Network Byte Order)
            Array.Reverse(buffer, abcProtoHdr.Length, sizeof(ushort));

            //Copy Data into buffer 
            data.Read(buffer, abcProtoHdr.Length + sizeof(ushort), buffer.Length - 4); 
            
            // Call the original OnOutput() using the buffer as our new BitStream
            base.OnOutput(new BitStream(buffer)); 
            
        }
	}
}
