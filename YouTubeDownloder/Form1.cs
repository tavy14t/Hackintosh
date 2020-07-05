using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace YouTubeDownloder
{
    public partial class Form1 : Form
    {
        private static Mutex mut = new Mutex();
        string outputDir = "";
        Thread[] threads = null;
        Hashtable hashtable = new Hashtable();

        public Form1()
        {
            InitializeComponent();
        }

        private void LoadYoutubeDl()
        {
            richTextBox1.Text += "Loading youtube-dl...\n";
            if (!File.Exists("youtube-dl.exe"))
            {
                richTextBox1.Text += "youtube-dl.exe does not exist!\n";
            }
            else
            {
                ProcessStartInfo pinfo = new ProcessStartInfo("youtube-dl.exe", "--update");
                pinfo.CreateNoWindow = true;
                pinfo.WindowStyle = ProcessWindowStyle.Hidden;
                var oldSize = new FileInfo("youtube-dl.exe").Length;
                var proc = Process.Start(pinfo);
                proc.WaitForExit();
                var newSize = new FileInfo("youtube-dl.exe").Length;
                if (newSize != oldSize)
                    richTextBox1.Text += "Youtube-dl updated!\n";
                else
                    richTextBox1.Text += "Latest youtube-dl available!\n";
            }
        }

        private void LoadFfmpeg()
        {
            var pinfo = new ProcessStartInfo("where", "ffmpeg.exe");
            pinfo.CreateNoWindow = true;
            pinfo.WindowStyle = ProcessWindowStyle.Hidden;

            using (Process proc = Process.Start(pinfo))
            {
                proc.WaitForExit();
                if (proc.ExitCode != 0)
                    richTextBox1.Text += "ffmpeg.exe does not exist!\n";
                else
                    richTextBox1.Text += "ffmpeg.exe available!\n";
            }
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            LoadYoutubeDl();
            LoadFfmpeg();
            textBox2.Text = Directory.GetCurrentDirectory();
            outputDir = textBox2.Text;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (textBox1.Text.Length <= 2)
                return;

            listBox1.Items.Add(textBox1.Text);
            textBox1.Text = "";
        }

        private void textBox1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                button1_Click(null, null);
        }

        private void DownloadVideo(string url)
        {
            bool audio = radioButton1.Checked;

            string parameters = "";
            if (!audio)
                parameters += "-f ''(bestvideo+bestaudio/best)[protocol^=http]'' ";
            else
                parameters += "-f ''(worstvideo+bestaudio/best)[protocol^=http]'' -x --audio-format mp3 ";

            parameters += String.Format(@"-o ""{0}\\%(title)s.%(ext)s"" ", outputDir);
            parameters += url;


            var pinfo = new ProcessStartInfo("youtube-dl.exe", parameters);
            pinfo.WindowStyle = ProcessWindowStyle.Hidden;
            pinfo.UseShellExecute = true;

            var proc = Process.Start(pinfo);
            proc.WaitForExit();

            richTextBox1.BeginInvoke((Action)(() =>
            {
                mut.WaitOne();
                richTextBox1.Text += String.Format(@"Downloaded {0}", url) + "\n";
                mut.ReleaseMutex();
            }));
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (threads != null)
                return;
            threads = new Thread[(int)numericUpDown1.Value];
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(() =>
                {
                    var tid = Thread.CurrentThread.ManagedThreadId;

                    listBox2.BeginInvoke((Action)(() =>
                    {
                        listBox2.Items.Add(String.Format(@"Thread-{0} running", tid));
                        hashtable[tid] = listBox2.Items.Count - 1;
                    }));

                    while (true)
                    {
                        if (listBox1.Items.Count == 0)
                        {
                            Thread.Sleep(1000);
                            continue;
                        }

                        var url = (string)listBox1.Invoke(new Func<string>(() =>
                       {
                           mut.WaitOne();
                           if (listBox1.Items.Count == 0) return null;
                           var ret = listBox1.Items[listBox1.Items.Count - 1].ToString();
                           listBox1.Items.RemoveAt(listBox1.Items.Count - 1);
                           mut.ReleaseMutex();
                           return ret;
                       }));

                        if (url != null)
                        {

                            Delegate del = (Action<string>)((step) =>
                            {
                                mut.WaitOne();
                                listBox2.Items[(int)hashtable[tid]] = String.Format(@"Thread-{0} {1}", tid, step);
                                mut.ReleaseMutex();
                            });

                            listBox2.Invoke(del, "downloading");
                            DownloadVideo(url);
                            listBox2.Invoke(del, "running");
                        }

                        Thread.Sleep(1000);
                    }
                });
                threads[i].Start();
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < threads?.Length; i++)
            {
                listBox2.Items[i] = String.Format(@"Thread-{0} stopped", i);
                threads[i].Abort();
            }
            threads = null;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            button3_Click(null, null);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            DialogResult result = folderBrowserDialog1.ShowDialog();

            if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(folderBrowserDialog1.SelectedPath))
            {
                outputDir = folderBrowserDialog1.SelectedPath;
                textBox2.Text = outputDir;
            }
        }
    }
}
