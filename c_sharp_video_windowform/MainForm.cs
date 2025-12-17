using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Speech.Synthesis;
using NAudio.Wave;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace c_sharp_video_windowform
{
    public partial class MainForm : Form
    {
        string ffmpegPath = @"D:\c_sharp_video_windowform\c_sharp_video_windowform\ffmpeg\bin\ffmpeg.exe";

        public MainForm()
        {
            InitializeComponent();
        }

        // SELECT TEXT FILE
        private void BtnSelectTxt_Click(object sender, EventArgs e)
        {
            using OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "Text Files (*.txt)|*.txt"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
                txtTextFile.Text = ofd.FileName;
        }

        // SELECT IMAGE FILE
        private void BtnSelectImage_Click(object sender, EventArgs e)
        {
            using OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "Image Files (*.png;*.jpg)|*.png;*.jpg"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
                txtImageFile.Text = ofd.FileName;
        }

        // GENERATE VIDEO
        private async void BtnGenerate_Click(object sender, EventArgs e)
        {
            string txtFile = txtTextFile.Text;
            string imageFile = txtImageFile.Text;

            if (!File.Exists(txtFile) || !File.Exists(imageFile))
            {
                MessageBox.Show("Please select both Text and Image files.");
                return;
            }

            lblStatus.Text = "Generating video, please wait...";

            try
            {
                await Task.Run(() => GenerateVideo(txtFile, imageFile));
                lblStatus.Text = "✅ Video generated successfully!";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "❌ Error!";
                MessageBox.Show(ex.Message);
            }
        }

        private void GenerateVideo(string txtFile, string imageFile)
        {
            string text = File.ReadAllText(txtFile);

            string outputFolder = Path.GetDirectoryName(txtFile);
            string outputVideo = Path.Combine(
                outputFolder,
                Path.GetFileNameWithoutExtension(txtFile) + "_VIDEO.mp4");

            string srtFile = Path.Combine(
                outputFolder,
                Path.GetFileNameWithoutExtension(txtFile) + ".srt");

            string mp3File = Path.Combine(Path.GetTempPath(), "audio.mp3");
            string tempVideo = Path.Combine(Path.GetTempPath(), "tempVideo.mp4");

            GenerateAudioAndSrt(text, mp3File, srtFile, 10);

            double audioDuration = GetAudioDuration(mp3File);

            int videoWidth = 1920;
            int videoHeight = 1080;

            string scaledImage = imageFile;
            bool isTempScaledImage = false;

            if (!IsImageSizeMatch(imageFile, videoWidth, videoHeight))
            {
                MessageBox.Show(
                    $"Input image must be exactly {videoWidth}x{videoHeight}. It will be resized.");

                scaledImage = PreScaleImageToExactSize(imageFile, videoWidth, videoHeight);
                isTempScaledImage = true;
            }

            RunFFmpeg(
                $"-loop 1 -i \"{scaledImage}\" -i \"{mp3File}\" " +
                $"-c:v libx264 -preset fast -crf 23 -tune stillimage " +
                $"-c:a aac -b:a 192k -pix_fmt yuv420p " +
                $"-t {audioDuration} -y \"{tempVideo}\""
            );

            string srtEscaped = srtFile.Replace("\\", "\\\\").Replace(":", "\\:");

            RunFFmpeg(
                $"-i \"{tempVideo}\" -vf " +
                $"\"subtitles='{srtEscaped}':force_style=" +
                $"'FontName=Arial,FontSize=26,PrimaryColour=&H00FFFFFF," +
                $"OutlineColour=&H00000000,BorderStyle=1,Outline=3," +
                $"Shadow=0,Alignment=2,MarginV=30'\" " +
                $"-c:v libx264 -crf 23 -preset fast -c:a copy " +
                $"-y \"{outputVideo}\""
            );

            // Cleanup
            if (File.Exists(tempVideo)) File.Delete(tempVideo);
            if (File.Exists(mp3File)) File.Delete(mp3File);
            if (isTempScaledImage && File.Exists(scaledImage)) File.Delete(scaledImage);

            MessageBox.Show(
                $"1️⃣ Video created successfully:\n{outputVideo}\n\n" +
                $"2️⃣ SRT created successfully:\n{srtFile}");
        }

        // ========== UTILITIES ==========

        double GetAudioDuration(string audioFile)
        {
            using var reader = new AudioFileReader(audioFile);
            return reader.TotalTime.TotalSeconds;
        }

        bool IsImageSizeMatch(string imgPath, int targetWidth, int targetHeight)
        {
            using var img = Image.FromFile(imgPath);
            return img.Width == targetWidth && img.Height == targetHeight;
        }

        void GenerateAudioAndSrt(string text, string mp3, string srt, int wordsPerBlock)
        {
            StringBuilder sb = new StringBuilder();
            TimeSpan currentTime = TimeSpan.Zero;
            int index = 1;

            var blockWavs = new System.Collections.Generic.List<string>();

            string[] sentences = text.Split(
                new[] { '.', '!', '?' },
                StringSplitOptions.RemoveEmptyEntries);

            foreach (string sentence in sentences)
            {
                string[] words = sentence.Trim()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < words.Length; i += wordsPerBlock)
                {
                    string block = string.Join(
                        " ",
                        words,
                        i,
                        Math.Min(wordsPerBlock, words.Length - i));

                    string wav = Path.Combine(
                        Path.GetTempPath(),
                        $"block_{Guid.NewGuid():N}.wav");

                    using (var synth = new SpeechSynthesizer())
                    {
                        synth.SelectVoice("Microsoft Mark");
                        synth.Rate = -4;
                        synth.Volume = 100;
                        synth.SetOutputToWaveFile(wav);
                        synth.Speak(block);
                    }

                    TimeSpan duration;
                    using (var reader = new AudioFileReader(wav))
                        duration = reader.TotalTime;

                    sb.AppendLine(index.ToString());
                    sb.AppendLine($"{FormatTime(currentTime)} --> {FormatTime(currentTime + duration)}");
                    sb.AppendLine(block);
                    sb.AppendLine();

                    currentTime += duration;
                    index++;

                    blockWavs.Add(wav);
                }
            }

            File.WriteAllText(srt, sb.ToString());

            string listFile = Path.Combine(Path.GetTempPath(), "concat.txt");

            using (var w = new StreamWriter(listFile))
                foreach (string f in blockWavs)
                    w.WriteLine($"file '{f}'");

            RunFFmpeg(
                $"-f concat -safe 0 -i \"{listFile}\" " +
                $"-c:a libmp3lame -q:a 4 -y \"{mp3}\"");

            foreach (string f in blockWavs)
                if (File.Exists(f)) File.Delete(f);

            if (File.Exists(listFile)) File.Delete(listFile);
        }

        string PreScaleImageToExactSize(string img, int targetWidth, int targetHeight)
        {
            string scaled = Path.Combine(
                Path.GetTempPath(),
                $"scaled_{Guid.NewGuid():N}.png");

            RunFFmpeg(
                $"-i \"{img}\" -vf " +
                $"\"scale={targetWidth}:{targetHeight},format=yuv420p\" " +
                $"-y \"{scaled}\"");

            return scaled;
        }

        string FormatTime(TimeSpan t)
        {
            return $"{t:hh\\:mm\\:ss\\,fff}";
        }

        void RunFFmpeg(string args)
        {
            using Process p = new Process();
            p.StartInfo.FileName = ffmpegPath;
            p.StartInfo.Arguments = args;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.CreateNoWindow = true;

            p.Start();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();

            if (p.ExitCode != 0)
                throw new Exception("FFmpeg failed:\n" + stderr);
        }
    }
}
