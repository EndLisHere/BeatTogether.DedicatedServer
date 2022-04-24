﻿using System;
using BeatTogether.DedicatedServer.Node.Abstractions;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BeatTogether.DedicatedServer.Node;
using BeatTogether.DedicatedServer.Kernel;
using GalaSoft.MvvmLight.Messaging;
using WinFormsLibrary.Messages;
using BeatTogether.DedicatedServer.Interface.Models;
using BeatTogether.DedicatedServer.Interface.Enums;
using BeatTogether.DedicatedServer.Interface;
using BeatTogether.DedicatedServer.Interface.Events;
using Autobus;

namespace WinFormsLibrary
{
    public partial class DedicatedServerViews : Form
    {
        private readonly IInstanceRegistry _instanceRegistry;
        private readonly IAutobus _autobus;

        public DedicatedServerViews(IInstanceRegistry _instanceRegistry, IAutobus _autobus)
        {
            InitializeComponent();
            this._instanceRegistry = _instanceRegistry;
            this._autobus = _autobus;
            DedicatedServerInstances.DataSource = ((InstanceRegistry)_instanceRegistry)._instances.ToList();
            Messenger.Default.Register<UpdateForm>(this, (action) => FormRecieveMsg());
            Messenger.Default.Register<LobbyUpdateMessage>(this, (action) => FormRecieveLobbyMsg(action));

            UpdateScreen();
        }

        public void FormRecieveMsg() {
            BeginInvoke((Action)delegate ()
            {
                UpdateScreen();
            });
        }

        public void FormRecieveLobbyMsg(LobbyUpdateMessage lobbyUpdateMessage)
        {
            BeginInvoke((Action)delegate ()
            {
                UpdatePlayerList();
                UpdateInstanceStats(lobbyUpdateMessage.GetCountdownEndTime());
            });
        }

        public void UpdateScreen()
        {
            DedicatedServerInstances.DataSource = null;
            DedicatedServerInstances.DataSource = ((InstanceRegistry)_instanceRegistry)._instances.Keys.ToList();
            UpdatePlayerList();
            UpdateInstanceStats(0);
        }


        private void UpdateInstanceStats(float countdownendtime)
        {
            if (DedicatedServerInstances.SelectedValue != null)
            {
                if (((InstanceRegistry)_instanceRegistry).TryGetInstance(DedicatedServerInstances.SelectedValue.ToString()!, out var instance))
                {
                    var selectedInstance = (DedicatedInstance)instance; MaxPlayers.Text = selectedInstance.Configuration.MaxPlayerCount.ToString() + " Max players";
                    PlayerCount.Text = selectedInstance.GetPlayerRegistry().Players.Count.ToString() + " Players";
                    Port.Text = selectedInstance.Configuration.Port.ToString();
                    RunTime.Text = "Running for: " + selectedInstance.RunTime.ToString() + " Seconds";
                    GameState.Text = selectedInstance.State.ToString();
                    ConnectionIDCount.Text = "ConnectionID count: " + selectedInstance.GetConnectionIDcount();
                    if (countdownendtime - selectedInstance.RunTime < 0)
                        Countdown.Text = ("Players downloading/loading: " + (countdownendtime - selectedInstance.RunTime).ToString());
                    else if (countdownendtime != 0)
                        Countdown.Text = (countdownendtime - selectedInstance.RunTime).ToString();
                    else 
                        Countdown.Text = ("Not counting down");
                    SongSelectionModeBox.Text = selectedInstance.Configuration.SongSelectionMode.ToString();
                    GameplayServerControlSettingBox.Text = selectedInstance.Configuration.GameplayServerControlSettings.ToString();
                    DiscoveryPolicyBox.Text = selectedInstance.Configuration.DiscoveryPolicy.ToString();
                    InvitePolicyBox.Text = selectedInstance.Configuration.InvitePolicy.ToString();
                    GameplayServerModeBox.Text = selectedInstance.Configuration.GameplayServerMode.ToString();
                }
                else
                {
                    MaxPlayers.Text = "NaN Max players";
                    PlayerCount.Text = "NaN Players";
                    Port.Text = "NAN";
                    RunTime.Text = "NAN";
                    GameState.Text = "NAN";
                    Countdown.Text = "NAN";
                    ConnectionIDCount.Text = "ConnectionID count: ";

                    SongSelectionModeBox.Text = "NAN";
                    GameplayServerControlSettingBox.Text = "NAN";
                    DiscoveryPolicyBox.Text = "NAN";
                    InvitePolicyBox.Text = "NAN";
                    GameplayServerModeBox.Text = "NAN";


                }
            }
        }

        private void UpdatePlayerList()
        {
            if(DedicatedServerInstances.SelectedValue != null)
            {
                if( ((InstanceRegistry)_instanceRegistry).TryGetInstance(DedicatedServerInstances.SelectedValue.ToString()!, out var instance))
                {
                    var selectedInstance = (DedicatedInstance)instance;
                    if (PlayersDataGrid.DataSource != selectedInstance.GetPlayerRegistry().Players)
                    {
                        PlayersDataGrid.DataSource = selectedInstance.GetPlayerRegistry().Players;
                        PlayersDataGrid.Columns["Random"].Visible = false;
                        PlayersDataGrid.Columns["Instance"].Visible = false;
                        PlayersDataGrid.Columns["RemoteConnectionId"].Visible = false;
                        PlayersDataGrid.Columns["PublicEncryptionKey"].Visible = false;
                        PlayersDataGrid.Columns["Latency"].Visible = false;
                        PlayersDataGrid.Columns["SyncTime"].Visible = false;
                        PlayersDataGrid.Columns["Avatar"].Visible = false;
                        PlayersDataGrid.Columns["BeatmapIdentifier"].Visible = false;
                        PlayersDataGrid.Columns["Modifiers"].Visible = false;
                        PlayersDataGrid.Columns["State"].Visible = false;
                        PlayersDataGrid.Columns["IsManager"].Visible = false;
                        PlayersDataGrid.Columns["CanRecommendBeatmaps"].Visible = false;
                        PlayersDataGrid.Columns["CanRecommendModifiers"].Visible = false;
                        PlayersDataGrid.Columns["CanKickVote"].Visible = false;
                        PlayersDataGrid.Columns["CanInvite"].Visible = false;
                        PlayersDataGrid.Columns["IsPlayer"].Visible = false;
                        PlayersDataGrid.Columns["IsBackgrounded"].Visible = false;
                        PlayersDataGrid.Columns["IsActive"].Visible = false;
                        PlayersDataGrid.Columns["InMenu"].Visible = false;
                        PlayersDataGrid.Columns["IsModded"].Visible = false;
                        PlayersDataGrid.Columns["Secret"].Visible = false;
                        PlayersDataGrid.Columns["WantsToPlayNextLevel"].Visible = false;
                        PlayersDataGrid.Columns["FinishedLevel"].Visible = false;
                        PlayersDataGrid.Columns["WasActiveAtLevelStart"].Visible = false;
                        PlayersDataGrid.RowHeadersVisible = false;

                        PlayersDataGrid.Columns["UserName"].DisplayIndex = 0;
                        PlayersDataGrid.Columns["UserName"].Width = 75;
                        PlayersDataGrid.Columns["InLobby"].DisplayIndex = 1;
                        PlayersDataGrid.Columns["InLobby"].Width = 55;
                        PlayersDataGrid.Columns["IsReady"].DisplayIndex = 2;
                        PlayersDataGrid.Columns["IsReady"].Width = 50;
                        PlayersDataGrid.Columns["UserID"].DisplayIndex = 3;
                        PlayersDataGrid.Columns["UserID"].Width = 60;
                        PlayersDataGrid.Columns["IsSpectating"].DisplayIndex = 4;
                        PlayersDataGrid.Columns["IsSpectating"].Width = 50;
                        PlayersDataGrid.Columns["InGameplay"].DisplayIndex = 5;
                        PlayersDataGrid.Columns["InGameplay"].Width = 50;
                        PlayersDataGrid.Columns["ConnectionId"].DisplayIndex = 6;
                        PlayersDataGrid.Columns["ConnectionId"].Width = 50;
                        PlayersDataGrid.Columns["SortIndex"].DisplayIndex = 7;
                        PlayersDataGrid.Columns["SortIndex"].Width = 50;
                        PlayersDataGrid.Columns["Endpoint"].DisplayIndex = 8;
                        PlayersDataGrid.Columns["Endpoint"].Width = 200;




                        if (selectedInstance.GetPlayerRegistry().TryGetPlayer(selectedInstance.Configuration.ManagerId, out var player))
                            InstanceManager.Text = "Custom instance, Manager is: " + player.UserName;
                        else if (selectedInstance.Configuration.ManagerId == "ziuMSceapEuNN7wRGQXrZg")
                            InstanceManager.Text = "QUICKPLAY instance";
                        else InstanceManager.Text = "Creating Instance";
                    }
                }
                else
                {
                    PlayersDataGrid.DataSource = null;
                    InstanceManager.Text = "None selected";
                }
            }
            else
            {
                PlayersDataGrid.DataSource = null;
                InstanceManager.Text = "No instances avaliable";
            }

        }

        private void AddInstanceButton_Click(object sender, EventArgs e)
        {
            if (RoomCodeTextBox.Text.Length == 5)
            {
                string secret = "SpecialServer" + RoomCodeTextBox.Text;

                GameplayServerConfiguration configuration = new GameplayServerConfiguration(500, DiscoveryPolicy.Public, InvitePolicy.AnyoneCanInvite, GameplayServerMode.Countdown, SongSelectionMode.Vote, GameplayServerControlSettings.All);

                _autobus.Publish(new FromServerCreateServerEvent(configuration, secret, RoomCodeTextBox.Text.ToUpper(), "ziuMSceapEuNN7wRGQXrZg"));
            }
        }

        private void StopSelectedInstance_Click(object sender, EventArgs e)
        {
            if (DedicatedServerInstances.SelectedValue != null)
            {
                if (((InstanceRegistry)_instanceRegistry).TryGetInstance(DedicatedServerInstances.SelectedValue.ToString()!, out var instance))
                ((DedicatedInstance)instance).StopDedicatedInstance();
            }

        }

        private void KickPlayerButton_Click(object sender, EventArgs e)
        {
            if(PlayersDataGrid.Rows.Count >= 1 && ((InstanceRegistry)_instanceRegistry).TryGetInstance(DedicatedServerInstances.SelectedValue.ToString()!, out var instance))
            {
                int SelectedIndex = PlayersDataGrid.SelectedCells[0].RowIndex;
                string userToKick = ((DedicatedInstance)instance).GetPlayerRegistry().Players[SelectedIndex].UserId;
                ((DedicatedInstance)instance).KickPlayer(userToKick);
            }
        }

        private void DedicatedServerInstances_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdatePlayerList();
            UpdateInstanceStats(0);
        }
    }
}
