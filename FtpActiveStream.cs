﻿using System;
using System.Net;
using System.Net.Sockets;

namespace System.Net.FtpClient {
    /// <summary>
    /// FtpDataStream setup for active mode transfers
    /// </summary>
	public class FtpActiveStream : FtpDataStream {
        /// <summary>
        /// Executes the specified command on the control connection
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
		public override FtpReply Execute(string command) {
			// if we're already connected we need
			// to reset ourselves and start over
			if(this.Socket.Connected) {
				this.Close();
			}

			if(!this.Socket.Connected) {
				this.Open();
			}

			try {

				this.ControlConnection.LockControlConnection();
				this.CommandReply = this.ControlConnection.Execute(command);

				if(this.CommandReply.Success && !this.Socket.Connected) {
					this.Accept();
				}
			}
			finally {
				this.ControlConnection.UnlockControlConnection();
			}

            return this.CommandReply;
		}

        /// <summary>
        /// Accepts the incomming connection
        /// </summary>
		protected void Accept() {
			this.Socket = this.Socket.Accept();
		}

        /// <summary>
        /// Opens the specified type of active data stream
        /// </summary>
        /// <param name="type"></param>
		protected override void Open(FtpDataChannelType type) {
            FtpReply reply;
			string ipaddress = null;
			int port = 0;

			this.Socket.Bind(new IPEndPoint(((IPEndPoint)this.ControlConnection.LocalEndPoint).Address, 0));
			this.Socket.Listen(1);

			ipaddress = ((IPEndPoint)this.Socket.LocalEndPoint).Address.ToString();
			port = ((IPEndPoint)this.Socket.LocalEndPoint).Port;

			try {
				this.ControlConnection.LockControlConnection();

				switch(type) {
					case FtpDataChannelType.ExtendedActive:
						reply = this.ControlConnection.Execute("EPRT |1|{0}|{1}|", ipaddress, port);
						if(reply.Type == FtpResponseType.PermanentNegativeCompletion) {
							this.ControlConnection.RemoveCapability(FtpCapability.EPSV);
							this.ControlConnection.RemoveCapability(FtpCapability.EPRT);
							reply = this.ControlConnection.Execute("PORT {0},{1},{2}",
								ipaddress.Replace(".", ","), port / 256, port % 256);
							type = FtpDataChannelType.Active;
						}
						break;
					case FtpDataChannelType.Active:
						reply = this.ControlConnection.Execute("PORT {0},{1},{2}",
							ipaddress.Replace(".", ","), port / 256, port % 256);
						break;
					default:
						throw new Exception("Active streams do not support " + type.ToString());
				}

				if(!reply.Success) {
					throw new FtpCommandException(reply);
				}
			}
			finally {
				this.ControlConnection.UnlockControlConnection();
			}
		}

        /// <summary>
        /// Initalizes a new instance of an active ftp data stream
        /// </summary>
        /// <param name="chan"></param>
		public FtpActiveStream(FtpControlConnection chan)
			: base() {
			if(chan == null) {
				throw new ArgumentNullException("chan");
			}
			this.ControlConnection = chan;
		}
	}
}
