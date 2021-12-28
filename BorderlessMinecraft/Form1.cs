//Copyright (C) 2019 Riley Nielsen

//This file is part of Borderless Minecraft.

//   Borderless Minecraft is free software: you can redistribute it and/or modify
//    it under the terms of the GNU General Public License as published by
//    the Free Software Foundation, either version 3 of the License, or
//    (at your option) any later version.

//    Borderless Minecraft is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.See the
//    GNU General Public License for more details.

//    You should have received a copy of the GNU General Public License
//    along with Borderless Minecraft.  If not, see<https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;
using System.Collections;
using BorderlessMinecraft.Configuration;
using System.Management;
using BorderlessMinecraft.Processes;
using static BorderlessMinecraft.DLLInterop; //includ the static DLL Interop class

// This is the code for your desktop app.
// Press Ctrl+F5 (or go to Debug > Start Without Debugging) to run your app.

namespace BorderlessMinecraft
{
    internal partial class Form1 : Form
    {
        int? AutoBorderlessPID; //the process id of an automatically set borderless window
        Process[] minecraftProcesses; //initializes an array of processess
        List<int> renamedProcesses = new List<int>(); //PIDs that are renamed
        Config Config { get; } = new Config();
        ProcessMonitor ProcessMonitor { get; }

        //protected override CreateParams CreateParams
        //{
        //    get
        //    {
        //        var parameters = base.CreateParams;
        //        parameters.ExStyle |= 0x80; //we need to hide the 
        //        return parameters;
        //    }
        //}

        internal Form1()
        {
            InitializeComponent();
            AddProcesses();

            //create the ToolTip for advanced mode hover
            ToolTip toolTip1 = new ToolTip();

            toolTip1.AutoPopDelay = 5000;
            toolTip1.InitialDelay = 1;
            toolTip1.ReshowDelay = 500;
            toolTip1.ShowAlways = true;

            toolTip1.SetToolTip(this.checkBox1, "Enables the use of custom positioning and sizing.");

            toolStripMenuItem1.Checked = Config.StartOnBoot;
            toolStripMenuItem2.Checked = Config.StartMinimized;
            toolStripMenuItem3.Checked = Config.MinimizeToTray;
            toolStripMenuItem4.Checked = Config.AutomaticBorderless;
            toolStripMenuItem5.Checked = Config.PreserveTaskBar;

            ProcessMonitor = new ProcessMonitor(); //create the process monitor
            ProcessMonitor.OnJavaAppStarted += ProcessMonitor_OnJavaAppStarted; //attach events
            ProcessMonitor.OnJavaAppStopped += ProcessMonitor_OnJavaAppStopped;
            ProcessMonitor.OnProcessShouldExit += ProcessMonitor_OnProcessShouldExit;
            if (Config.AutomaticBorderless) //start listening if auto borderless is enabled
                ProcessMonitor.Start(); //this call must occur after attaching the OnProcessShouldExit event

            //set up event handlers
            Resize += Form1_Resize1;

            TrayIcon.MouseClick += TrayIcon_Click;
            Exit.Click += Exit_Click;

            toolStripMenuItem1.CheckedChanged += StartOnBootItem_CheckedChanged;
            toolStripMenuItem2.CheckedChanged += StartMinimizedItem_CheckedChanged;
            toolStripMenuItem3.CheckedChanged += MinimizeToTrayItem_CheckedChanged;
            toolStripMenuItem4.CheckedChanged += AutoBorderlessItem_CheckedChanged;
            toolStripMenuItem5.CheckedChanged += PreserveTaskBarItem_CheckedChanged;

            if (Config.StartMinimized && IsAutoStarted()) //ensure the app has autostarted and minimize to tray is enabled. This ensures normal starts will not be minimized
            {
                WindowState = FormWindowState.Minimized;
                Hide(); //hide the app in the tray
                ShowInTaskbar = false; //When hiding on startup, we need to explicitly set ShowInTaskbar to false
                TrayIcon.Visible = true;
            }
        }

        private void ProcessMonitor_OnProcessShouldExit() => Environment.Exit(0);

        private async void ProcessMonitor_OnJavaAppStarted(int pid)
        {
            if (!AutoBorderlessPID.HasValue) //if no window has been made borderless, make the java app that just opened borderless
            {
                AutoBorderlessPID = pid; //store the pid
                IntPtr handle = default;
                bool HandleFound = false;
                Process process = null;
                while (!HandleFound)
                {
                    process = Process.GetProcessById(AutoBorderlessPID.Value); //get the process by ID
                    if (process.MainWindowHandle != default) //check if the handle exists
                    {
                        HandleFound = true;
                        handle = process.MainWindowHandle; //if yes, save the handle
                    }
                    await Task.Delay(250); //if no, delay 250 ms
                }

                if (process.MainWindowTitle.Contains("Minecraft")) //for now just check the title with a magic string. In the future this will allow better control. Also note that we have already filtered out any non-java process
                    GoBorderless(handle, 0, 0, GetScreenRezx(), GetScreenRezy()); //go borderless
            }
            AddProcessesThreadSafe();
        }

        private void ProcessMonitor_OnJavaAppStopped(int pid)
        {
            if (AutoBorderlessPID.HasValue) //if the app that was made borderless automatically has
            {
                AutoBorderlessPID = null; //reset the pid to null
            }
            AddProcessesThreadSafe();
        }

        private void TrayIcon_Click(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) //show the window when the tray icon is left clicked
            {
                Show();
                WindowState = FormWindowState.Normal;
                ShowInTaskbar = true;
            }
        }

        private void StartOnBootItem_CheckedChanged(object sender, EventArgs e) => Config.StartOnBoot = ((ToolStripMenuItem)sender).Checked;
        private void StartMinimizedItem_CheckedChanged(object sender, EventArgs e) => Config.StartMinimized = ((ToolStripMenuItem)sender).Checked;
        private void MinimizeToTrayItem_CheckedChanged(object sender, EventArgs e) => Config.MinimizeToTray = ((ToolStripMenuItem)sender).Checked;
        private void AutoBorderlessItem_CheckedChanged(object sender, EventArgs e)
        {
            var state = ((ToolStripMenuItem)sender).Checked;
            Config.AutomaticBorderless = state;
            if (state)
                ProcessMonitor.Start();
            if (!state) //if the mode is disabled, stop the process monitor
                ProcessMonitor.Stop();
        }
        private void PreserveTaskBarItem_CheckedChanged(object sender, EventArgs e) => Config.PreserveTaskBar = ((ToolStripMenuItem)sender).Checked;

        private void Exit_Click(object sender, EventArgs e) => Close();

        private void AddProcesses() //method to add processes to list
        {
            listBox1.Items.Clear(); //clear the listbox on refresh
            button1.Enabled = false; //disable the button by default
            button3.Enabled = false; //disable the button by default
            button4.Enabled = false; //disable the button by default

            minecraftProcesses = GetProcesses(renamedProcesses, filterBox.Text);

            foreach (Process proc in minecraftProcesses)
            {
                listBox1.Items.Add(proc.MainWindowTitle); //adds process title to list
            }
        }

        /// <summary>
        /// Calls AddProcesses internally after syncing back to the UI thread
        /// </summary>
        private void AddProcessesThreadSafe()
        {
            //sync back to UI thread https://stackoverflow.com/a/6691629/14024210
            Invoke(new MethodInvoker(AddProcesses));
        }

        private void debugInstructionsLabel_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e) //go borderless button
        {
            //default values, changed by advanced mode
            int xPos = 0;
            int yPos = 0;
            int xRes = GetScreenRezx();
            int yRes = GetScreenRezy();

            if (Config.PreserveTaskBar)
            {
                yRes = GetWorkingAreaHeight(); //ignored if advanced mode is enabled
            }

            //advanced mode checks
            if (textBox1.Text != "" && int.TryParse(textBox1.Text, out int parsed))
                xPos = parsed;
            if (textBox2.Text != "" && int.TryParse(textBox2.Text, out int parsed2))
                yPos = parsed2;
            if (textBox3.Text != "" && int.TryParse(textBox3.Text, out int parsed3))
                xRes = parsed3;
            if (textBox4.Text != "" && int.TryParse(textBox4.Text, out int parsed4))
                yRes = parsed4;
            IntPtr handle = minecraftProcesses[listBox1.SelectedIndex].MainWindowHandle; //gets the minecraft process by index, and then its handle
            int pid = minecraftProcesses[listBox1.SelectedIndex].Id; //get the minecraft process by id
            GoBorderless(handle, xPos, yPos, xRes, yRes);
            if (!AutoBorderlessPID.HasValue) //save this pid to effectively disable auto borderless. Otherwise a second instance of MC would also go borderless
                AutoBorderlessPID = pid;
        }

        private void button2_Click(object sender, EventArgs e) //refresh button
        {
            AddProcesses();
        }

        private void listBox1_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            if (listBox1.SelectedIndex > -1) //checks if something is selected
            {
                button1.Enabled = true; //enable the button when a session is seleced
                button3.Enabled = true; //enable the button when a session is seleced
                button4.Enabled = true; //enable the button when a session is seleced
            }
        }

        private void button3_Click(object sender, EventArgs e) //edit title button
        {
            IntPtr handle = minecraftProcesses[listBox1.SelectedIndex].MainWindowHandle; //gets the minecraft process by index, and then its handle
            int PID = minecraftProcesses[listBox1.SelectedIndex].Id;
            string currentTitle = minecraftProcesses[listBox1.SelectedIndex].MainWindowTitle; //gets the minecraft process by index, and then its title
            string title;
            if (textBox5.Text != "") //if the textbox has content, use for title
            {
                title = textBox5.Text;

            }
            else //if the textbox is empty, use default
            {
                title = currentTitle + " (Second Account)";
            }
            SetTitle(handle, title);
            if (!renamedProcesses.Contains(PID)) //if the renamed handle is not currently stored, add it. This allows borderless minecraft to detect windows that have been renamed
            {
                renamedProcesses.Add(PID);
            }

            AddProcesses(); //after rename, refresh the list
            textBox5.Text = ""; //reset text
        }

        private void button4_Click(object sender, EventArgs e) //restore window button
        {
            //default values, changed by advanced mode
            int xPos = GetCenterx();
            int yPos = GetCentery();
            int xRes = xDefaultRes;
            int yRes = yDefaultRes;

            if (textBox1.Text != "")
            {
                try
                {
                    xPos = Convert.ToInt32(textBox1.Text); //if there is content in the textbox, an attempt is made to assign it to variable
                }
                catch
                {
                    xPos = GetCenterx();
                }
            }
            if (textBox2.Text != "")
            {
                try
                {
                    yPos = Convert.ToInt32(textBox2.Text); //if there is content in the textbox, an attempt is made to assign it to variable
                }
                catch
                {
                    yPos = GetCentery();
                }
            }
            if (textBox3.Text != "")
            {
                try
                {
                    xRes = Convert.ToInt32(textBox3.Text); //if there is content in the textbox, an attempt is made to assign it to variable
                }
                catch
                {
                    xRes = xDefaultRes;
                }
            }
            if (textBox4.Text != "")
            {
                try
                {
                    yRes = Convert.ToInt32(textBox4.Text); //if there is content in the textbox, an attempt is made to assign it to variable
                }
                catch
                {
                    yRes = yDefaultRes;
                }
            }

            IntPtr handle = minecraftProcesses[listBox1.SelectedIndex].MainWindowHandle; //gets the minecraft process by index, and then its handle
            int pid = minecraftProcesses[listBox1.SelectedIndex].Id; //get the pid
            RestoreWindow(handle);
            UndoBorderless(handle);
            SetPos(handle, xPos, yPos, xRes, yRes);
            SetForeground(handle);
            if (AutoBorderlessPID == pid) //if the pid of the restored window matches our saved pid, set the saved pid to null
                AutoBorderlessPID = null;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked) //if advanced mode is on, make visible
            {
                textBox1.Visible = true;
                textBox2.Visible = true;
                textBox3.Visible = true;
                textBox4.Visible = true;
                label1.Visible = true;
                label2.Visible = true;
                label3.Visible = true;
                label4.Visible = true;
            }
            else
            {
                textBox1.Text = "";
                textBox2.Text = "";
                textBox3.Text = "";
                textBox4.Text = "";
                textBox1.Visible = false;
                textBox2.Visible = false;
                textBox3.Visible = false;
                textBox4.Visible = false;
                label1.Visible = false;
                label2.Visible = false;
                label3.Visible = false;
                label4.Visible = false;
            }
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            AddProcesses(); //refresh the process list when changing the filter option
        }

        private void Form1_Resize1(object sender, EventArgs e)
        {
            //if the form is minimized  
            //hide it from the task bar  
            //and show the system tray icon (represented by the NotifyIcon control)  
            if (Config.MinimizeToTray && WindowState == FormWindowState.Minimized)
            {
                Hide();
                TrayIcon.Visible = true;
            }
            else
            {
                TrayIcon.Visible = false; //we don't want the tray icon to show when window is normal
            }
        }

        /// <summary>
        /// Returns true if the -autoStart argument is included
        /// </summary>
        /// <returns></returns>
        private static bool IsAutoStarted()
        {
            string[] args = Environment.GetCommandLineArgs();
            return args.Contains("-autoStart");
        }

        private static Process[] GetProcesses(List<int> processIDs, string startsWith = "")
        {
            Process[] allProcesses = Process.GetProcesses(); //gets an array of all system processes

            List<Process> processes = allProcesses.Where(proc => proc.MainWindowTitle.StartsWith(startsWith) 
                                                                 && !string.IsNullOrWhiteSpace(proc.MainWindowTitle) 
                                                                 && !processIDs.Contains(proc.Id)).ToList();

            foreach (int PID in processIDs)
            {
                try
                {
                    processes.Add(Process.GetProcessById(PID)); //adds each process by ID
                }
                catch (ArgumentException) { }
            }
            return processes.Distinct().ToArray(); //converts dynamic arraylist to static array
        }

        private void filterBox_TextChanged(object sender, EventArgs e)
        {
            AddProcesses();
        }
    }
}
