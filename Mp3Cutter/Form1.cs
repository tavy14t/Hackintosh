using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;
using System.Windows.Forms;
using NAudio.Gui;
using NAudio.Wave.SampleProviders;
using System.IO;

namespace Mp3Cutter
{
    public partial class Form1 : Form
    {
        private WaveStream pcm = null;
        private Mp3FileReader mp3FileReader = null;
        private WaveOut output = null;
        private BlockAlignReductionStream stream = null;
        private int granularity = 250;

        Timer timer = null;

        public Form1()
        {
            InitializeComponent();

        }

        private void loadButton_Click(object sender, EventArgs e)
        {
            progressBar1.Value = 0;

            if (openFileDialog1.ShowDialog() != DialogResult.OK)
                return;

            output?.Stop();
            output?.Dispose();
            timer?.Stop();

            var fname = openFileDialog1.FileName;

            if (fname.EndsWith(".mp3"))
            {
                mp3FileReader = new Mp3FileReader(fname);
                pcm = WaveFormatConversionStream.CreatePcmStream(mp3FileReader);
            }

            else
            {
                MessageBox.Show("File is not mp3!");
                return;
            }

            granularity = (int) numericUpDown1.Value;
            textBoxMp3.Text = fname;

            stream = new BlockAlignReductionStream(pcm);
            output = new WaveOut();
            output.DesiredLatency = 500;
            output.Init(stream);

            waveViewer1.WaveStream = stream;


            trackBar1.Maximum = (int)(stream.TotalTime.TotalSeconds * (1000.0 / granularity));
            trackBar1.Value = 0;

            trackBar2.Maximum = (int)(stream.TotalTime.TotalSeconds * (1000.0 / granularity));
            trackBar2.Value = 0;

            trackBar3.Maximum = (int)(stream.TotalTime.TotalSeconds * (1000.0 / granularity));
            trackBar3.Value = trackBar3.Maximum;

            timer = new Timer();
            timer.Interval = (int)(granularity / 2 - 10);
            timer.Tick += new EventHandler(timer_Tick);
            
            textBox1.Text = TimeSpan.FromMilliseconds(0).ToShortForm();
            textBox2.Text = TimeSpan.FromMilliseconds(stream.TotalTime.TotalMilliseconds).ToShortForm();
        }

        public void timer_Tick(object sender, EventArgs e)
        {
            double ms = stream.GetMs(output);
            trackBar1.Value = (int)(ms / granularity);

            if (trackBar1.Value >= trackBar3.Value)
                output.Stop();

            if (output.PlaybackState == PlaybackState.Stopped)
                timer.Stop();

            textBox3.Text = TimeSpan.FromMilliseconds(ms).ToShortForm();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "mp3 files (*.mp3)|*.mp3";
            openFileDialog1.RestoreDirectory = true;

            trackBar3.Value = trackBar3.Maximum;

            numericUpDown1.Value = 250;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (output == null)
                return;

            double ms = trackBar1.GetMs(granularity);
            stream.Position = (long)(ms * output.OutputWaveFormat.SampleRate * output.OutputWaveFormat.BitsPerSample * output.OutputWaveFormat.Channels / 8000.0) & ~1;

            output?.Play();
            timer?.Start();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            timer?.Stop();
            output?.Pause();
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            if (stream == null)
                return;

            double ms = trackBar1.GetMs(granularity);
            stream.Position = (long)(ms * output.OutputWaveFormat.SampleRate * output.OutputWaveFormat.BitsPerSample * output.OutputWaveFormat.Channels / 8000.0) & ~1;
        }

        private void trackBar2_Scroll(object sender, EventArgs e)
        {
            if (trackBar2.Value >= trackBar3.Value)
                trackBar2.Value = trackBar3.Value - 1;

            trackBar1.Value = trackBar2.Value;

            trackBar1_Scroll(null, null);
            textBox1.Text = TimeSpan.FromMilliseconds(trackBar2.GetMs(granularity)).ToShortForm();
        }

        private void trackBar2_MouseDown(object sender, MouseEventArgs e)
        {
            trackBar2_Scroll(null, null);
            if (output?.PlaybackState == PlaybackState.Stopped)
            {
                output.Play();
                timer.Start();
            }
        }

        private void trackBar3_Scroll(object sender, EventArgs e)
        {
            if (trackBar3.Value <= trackBar2.Value)
                trackBar3.Value = trackBar2.Value + 1;

            textBox2.Text = TimeSpan.FromMilliseconds(trackBar3.GetMs(granularity)).ToShortForm();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            saveFileDialog1.FileName = openFileDialog1.FileName.Replace(".mp3", "_cut.mp3");

            if (saveFileDialog1.ShowDialog() != DialogResult.OK)
                return;

            var start = trackBar2.GetMs(granularity);
            var end = trackBar3.GetMs(granularity);

            try
            {
                using (var reader = new Mp3FileReader(openFileDialog1.FileName))
                using (var writer = File.Create(saveFileDialog1.FileName))
                {
                    progressBar1.Maximum = (int)reader.TotalTime.TotalSeconds;

                    if (reader.Id3v2Tag != null && checkBox1.Checked)
                        writer.Write(reader.Id3v2Tag.RawData, 0, reader.Id3v2Tag.RawData.Length);

                    Mp3Frame frame;
                    while ((frame = reader.ReadNextFrame()) != null)
                        if (reader.CurrentTime >= TimeSpan.FromMilliseconds(start))
                        {
                            progressBar1.Value = (int)reader.CurrentTime.TotalSeconds;

                            if (reader.CurrentTime <= TimeSpan.FromMilliseconds(end))
                                writer.Write(frame.RawData, 0, frame.RawData.Length);
                            else break;
                        }

                    progressBar1.Value = progressBar1.Maximum;
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            trackBar2_Scroll(null, null);
            output?.Play();
            if (output != null)
                timer.Start();
        }

    }

    static class Extensions
    {
        public static string ToShortForm(this TimeSpan t)
        {
            string shortForm = "";
            if (t.Hours > 0)
            {
                shortForm += string.Format("{0}:", t.Hours.ToString());
            }
            
            shortForm += string.Format("{0}:{1}:{2}", t.Minutes, t.Seconds, t.Milliseconds);
            
            return shortForm;
        }
        public static double GetMs(this NAudio.Wave.BlockAlignReductionStream stream, WaveOut output)
        {
            return stream.Position * 1000.0 / output.OutputWaveFormat.BitsPerSample / output.OutputWaveFormat.Channels * 8.0 / output.OutputWaveFormat.SampleRate;
        }
        public static double GetMs(this System.Windows.Forms.TrackBar tb, int granularity)
        {
            return tb.Value * granularity;
        }
    }
}
