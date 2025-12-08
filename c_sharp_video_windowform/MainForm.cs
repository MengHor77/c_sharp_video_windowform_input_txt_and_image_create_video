using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Speech.Synthesis;
using NAudio.Wave;
using System.Drawing;
using System.Windows.Forms;

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
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Text Files (*.txt)|*.txt";

            if (ofd.ShowDialog() == DialogResult.OK)
                txtTextFile.Text = ofd.FileName;
        }

        // SELECT IMAGE FILE
        private void BtnSelectImage_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Image Files (*.png;*.jpg)|*.png;*.jpg";

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

            await Task.Run(() => GenerateVideo(txtFile, imageFile));

            lblStatus.Text = "✅ Video generated successfully!";
        }

        private void GenerateVideo(string txtFile, string imageFile)
        {
            string text = File.ReadAllText(txtFile);

            // Determine output video path
            string outputFolder = Path.GetDirectoryName(txtFile); // save in same folder as input txt
            string outputVideo = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(txtFile) + "_VIDEO.mp4");

            // Save SRT in the same folder as the video
            string srtFile = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(txtFile) + ".srt");

            // MP3 can stay in temp
            string mp3File = Path.Combine(Path.GetTempPath(), "audio.mp3");
            string tempVideo = Path.Combine(Path.GetTempPath(), "tempVideo.mp4");

            // Generate audio and subtitles
            GenerateAudioAndSrt(text, mp3File, srtFile, 10);

            double audioDuration = GetAudioDuration(mp3File);

            int videoWidth = 1920;
            int videoHeight = 820;

            // Check if input image size matches target video size
            string scaledImage;
            if (!IsImageSizeMatch(imageFile, videoWidth, videoHeight))
            {
                // Option 1: warn user
                MessageBox.Show($"Input image must be exactly {videoWidth}x{videoHeight} pixels. Automatically resizing it.");

                // Option 2: resize automatically
                scaledImage = PreScaleImageToExactSize(imageFile, videoWidth, videoHeight);
            }
            else
            {
                scaledImage = imageFile; // no scaling needed
            }

            // Create video from image + audio
            RunFFmpeg(
                $"-loop 1 -i \"{scaledImage}\" -i \"{mp3File}\" " +
                $"-c:v libx264 -preset fast -crf 23 -tune stillimage " +
                $"-c:a aac -b:a 192k -pix_fmt yuv420p -t {audioDuration} -y \"{tempVideo}\""
            );

            // Overlay subtitles
            string srtEscaped = srtFile.Replace("\\", "\\\\").Replace(":", "\\:");
            RunFFmpeg(
                $"-i \"{tempVideo}\" -vf " +
                $"\"subtitles='{srtEscaped}':force_style=" +
                $"'FontName=Arial,FontSize=26,PrimaryColour=&H00FFFFFF,OutlineColour=&H00000000," +
                $"BorderStyle=1,Outline=3,Shadow=0,Alignment=2,MarginV=30'\" " +
                $"-c:v libx264 -crf 23 -preset fast -c:a copy -y \"{outputVideo}\""
            );

            // Cleanup
            File.Delete(tempVideo);
            File.Delete(mp3File);
            File.Delete(scaledImage);

            MessageBox.Show($"-1 Video  create successful! in : {outputVideo} \n\n -2 SRT Created successful! in :{srtFile}");
        }


        // ========== UTILITIES ==========

        double GetAudioDuration(string audioFile)
        {
            using var reader = new AudioFileReader(audioFile);
            return reader.TotalTime.TotalSeconds;
        }

        bool IsImageSizeMatch(string imgPath, int targetWidth, int targetHeight)
        {
            using (var img = Image.FromFile(imgPath))
            {
                return img.Width == targetWidth && img.Height == targetHeight;
            }
        }

        void GenerateAudioAndSrt(string text, string mp3, string srt, int wordsPerBlock)
        {
            StringBuilder sb = new StringBuilder();
            TimeSpan currentTime = TimeSpan.Zero;
            int index = 1;

            string[] sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            var blockWavs = new System.Collections.Generic.List<string>();

            foreach (var s in sentences)
            {
                string[] words = s.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < words.Length; i += wordsPerBlock)
                {
                    int end = Math.Min(i + wordsPerBlock, words.Length);
                    string block = string.Join(" ", words, i, end - i);

                    string wav = Path.Combine(Path.GetTempPath(), $"block_{Guid.NewGuid():N}.wav");

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
                foreach (var f in blockWavs)
                    w.WriteLine($"file '{f}'");

            RunFFmpeg($"-f concat -safe 0 -i \"{listFile}\" -c:a libmp3lame -q:a 4 -y \"{mp3}\"");

            foreach (var f in blockWavs) File.Delete(f);
            File.Delete(listFile);
        }

        string PreScaleImageToExactSize(string img, int targetWidth, int targetHeight)
        {
            string scaled = Path.Combine(Path.GetTempPath(), $"scaled_{Guid.NewGuid():N}.png");

            // Simple scale to exact size (may stretch if aspect ratio differs)
            RunFFmpeg(
                $"-i \"{img}\" -vf \"scale={targetWidth}:{targetHeight},format=yuv420p\" -y \"{scaled}\""
            );

            return scaled;
        }


        string FormatTime(TimeSpan t)
        {
            return $"{t:hh\\:mm\\:ss\\,fff}";
        }

        void RunFFmpeg(string args)
        {
            Process p = new Process();
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
                throw new Exception("FFmpeg failed: " + stderr);
        }
    }
}
