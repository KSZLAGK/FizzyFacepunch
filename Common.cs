﻿using Steamworks;
using System;
using System.Collections;
using UnityEngine;

namespace Mirror.FizzySteam
{
    public abstract class Common
    {
        private P2PSend[] channels;
        private int internal_ch => channels.Length;

        protected enum InternalMessages : byte
        {
            CONNECT,
            ACCEPT_CONNECT,
            DISCONNECT
        }

        protected readonly FizzyFacepunch transport;

        protected Common(FizzyFacepunch transport)
        {
            channels = transport.Channels;

            SteamNetworking.OnP2PSessionRequest += OnNewConnection;
            SteamNetworking.OnP2PConnectionFailed += OnConnectFail;

            this.transport = transport;
        }

        protected IEnumerator WaitDisconnect(SteamId steamID)
        {
            yield return new WaitForSeconds(0.1f);
            CloseP2PSessionWithUser(steamID);
        }

        protected void Dispose()
        {
            SteamNetworking.OnP2PSessionRequest -= OnNewConnection;
            SteamNetworking.OnP2PConnectionFailed -= OnConnectFail;
        }

        protected abstract void OnNewConnection(SteamId steamID);

        private void OnConnectFail(SteamId id, P2PSessionError err)
        {
            switch (err)
            {
                case P2PSessionError.NotRunningApp:
                    throw new Exception("Connection failed: The target user is not running the same game.");
                case P2PSessionError.NoRightsToApp:
                    throw new Exception("Connection failed: The local user doesn't own the app that is running.");
                case P2PSessionError.DestinationNotLoggedIn:
                    throw new Exception("Connection failed: Target user isn't connected to Steam.");
                case P2PSessionError.Timeout:
                    throw new Exception("Connection failed: The connection timed out because the target user didn't respond.");
                default:
                    throw new Exception("Connection failed: Unknown error.");
            }
        }

        protected void SendInternal(SteamId target, InternalMessages type) => SteamNetworking.SendP2PPacket(target, new byte[] { (byte) type }, 1, internal_ch, P2PSend.Reliable);
        protected bool Send(SteamId host, byte[] msgBuffer, int channel) => SteamNetworking.SendP2PPacket(host, msgBuffer, msgBuffer.Length, channel, channels[channel]);
        private bool Receive(out SteamId clientSteamID, out byte[] receiveBuffer, int channel)
        {
            if (SteamNetworking.IsP2PPacketAvailable(channel))
            {
                var data = SteamNetworking.ReadP2PPacket(channel);

                if (data != null)
                {
                    receiveBuffer = data.Value.Data;
                    clientSteamID = data.Value.SteamId;
                }
            }

            receiveBuffer = null;
            clientSteamID = 0;
            return false;
        }

        protected void CloseP2PSessionWithUser(SteamId clientSteamID) => SteamNetworking.CloseP2PSessionWithUser(clientSteamID);

        public void ReceiveData()
        {
            try
            {
                for (int chNum = 0; chNum < channels.Length; chNum++)
                {
                    while (Receive(out SteamId clientSteamID, out byte[] receiveBuffer, chNum))
                    {
                        OnReceiveData(receiveBuffer, clientSteamID, chNum);
                    }
                }

                while (Receive(out SteamId clientSteamID, out byte[] internalMessage, internal_ch))
                {
                    if (internalMessage.Length == 1)
                    {
                        OnReceiveInternalData((InternalMessages)internalMessage[0], clientSteamID);
                    }
                    else
                    {
                        Debug.Log("Incorrect package length on internal channel.");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        protected abstract void OnReceiveInternalData(InternalMessages type, SteamId clientSteamID);
        protected abstract void OnReceiveData(byte[] data, SteamId clientSteamID, int channel);
    }
}
