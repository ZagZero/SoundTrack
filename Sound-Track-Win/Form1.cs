using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Net.Http;
using Sound_Track_Win.RestAPI;
using Sound_Track_Win.NetworkAudio;

namespace Sound_Track_Win
{
    public partial class formST : Form
    {
        userSettingsForm userSettings;
        AudioReceiver audioHandle;
        SoundTrackRestHandler stRest;

        //Save-related values
        string settingsFile = "settings.csv";
        string UserID;
        string serverIP;

        //Status values
        bool connectedToServer;
        bool receivingAudio;
        

        public formST()
        {
            InitializeComponent();

            audioHandle = new AudioReceiver("Freedom");

            //stRest = new SoundTrackRestHandler("192.168.0.107");

            StartPosition = FormStartPosition.Manual;
            Location = new Point(Screen.PrimaryScreen.WorkingArea.Width - Width,
                                   Screen.PrimaryScreen.WorkingArea.Height - Height);

        }

        private void formST_SizeChanged(object sender, EventArgs e)
        {

            //Hide the form to remove it from taskbar when minimized
            if (this.WindowState == FormWindowState.Minimized)
            {
                Hide();
                notifyIconST.Visible = true;
            }
        }

        private void showForm(object sender, EventArgs e)
        {
            Show();
            this.WindowState = FormWindowState.Normal;
            notifyIconST.Visible = false;
        }

        private void closeForm(object sender, EventArgs e)
        {
            Close();
        }

        private void btnUser_Click(object sender, EventArgs e)
        {
            if (userSettings == null)
            {
                userSettings = new userSettingsForm(stRest);
                userSettings.ShowDialog();
            }
            else if (!userSettings.Visible)
            {
                userSettings = new userSettingsForm(stRest);
                userSettings.ShowDialog();
            }
        }

        private void UpdateStatusText(string status)
        {
            statusDisplay.Invoke((MethodInvoker)delegate
            {
                statusDisplay.Text = status;
                notifyIconST.Text = "Sound Track - Status: " + status;
            });
        }

        private void FormST_Load(object sender, EventArgs e)
        {
            audioWorker.DoWork += AudioWork;
            audioWorker.RunWorkerAsync();
            restBTWorker.DoWork += RestBTWork;
            restBTWorker.RunWorkerAsync();
        }

        //----------------------------------
        //   Methods for background work
        //----------------------------------

        private void AudioWork(object sender, DoWorkEventArgs e)
        {

        }

        private void RestBTWork(object sender, DoWorkEventArgs e)
        {

            UpdateStatusText("Connecting to server...");

            List<ServerResource> servers = audioHandle.PollServers();
            string serverNames = "";
            if (servers != null)
            {
                for (int i = 0; i < servers.Count; i++)
                {
                    serverNames += string.Format("{0} - {1}:{2} ({3})\n", servers[i].Name, servers[i].IP.ToString(), servers[i].CommPort, servers[i].ID);
                }
            }
            MessageBox.Show(serverNames);

            TimeResource serverTime = null;

            while (serverTime == null)
            {
                try { serverTime = stRest.GetServerTime(); }
                catch
                {
                    UpdateStatusText("Connection failed, retrying...");
                    Thread.Sleep(5000);
                    UpdateStatusText("Connecting to server...");
                }

            }
            UpdateStatusText("Connected");
            if (rbOutput.Checked)
            {

            }
            else if (rbTracker.Checked)
            {

            }
            else
            {
                //Shouldn't actually ever reach here
            }
        }
    }
}
