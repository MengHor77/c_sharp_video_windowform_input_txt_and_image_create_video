using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using NAudio.Wave;

namespace c_sharp_video_windowform
{
    public partial class MainForm : Form
    {
        string ffmpegPath = @"D:\c_sharp_video_windowform\c_sharp_video_windowform\ffmpeg\bin\ffmpeg.exe";

        public MainForm()
        {
            InitializeComponent();
        }

        // ===== SELECT TEXT FILE =====
        private void BtnSelectTxt_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Text Files (*.txt)|*.txt";

            if (ofd.ShowDialog() == DialogResult.OK)
                txtTextFile.Text = ofd.FileName;
        }

        // ===== SELECT IMAGE FILE =====
        private void BtnSelectImage_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Image Files (*.png;*.jpg)|*.png;*.jpg";

            if (ofd.ShowDialog() == DialogResult.OK)
                txtImageFile.Text = ofd.FileName;
        }

        // ===== GENERATE VIDEO =====
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
            await System.Threading.Tasks.Task.Run(() => GenerateVideo(txtFile, imageFile));
            lblStatus.Text = "✅ Video generated successfully!";
        }

        private void GenerateVideo(string txtFile, string imageFile)
        {
            string text = File.ReadAllText(txtFile);

            // Output paths
            string outputFolder = Path.GetDirectoryName(txtFile);
            string outputVideo = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(txtFile) + "_VIDEO.mp4");
            string srtFile = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(txtFile) + ".srt");

            string tempVideo = Path.Combine(Path.GetTempPath(), "tempVideo.mp4");

            int videoWidth = 1920;
            int videoHeight = 1080;

            // 1️⃣ Generate human-like audio and SRT
            string finalAudio = Path.Combine(Path.GetTempPath(), "final_audio.wav");
            GenerateAudioAndSrtByPhrase(text, finalAudio, srtFile);

            // 2️⃣ Resize image if needed
            string scaledImage = imageFile;
            if (!IsImageSizeMatch(imageFile, videoWidth, videoHeight))
            {
                MessageBox.Show($"Input image must be {videoWidth}x{videoHeight}. Resizing automatically.");
                scaledImage = PreScaleImageToExactSize(imageFile, videoWidth, videoHeight);
            }

            // 3️⃣ Create video with audio
            RunFFmpeg(
                $"-loop 1 -i \"{scaledImage}\" -i \"{finalAudio}\" " +
                $"-c:v libx264 -preset fast -crf 23 -tune stillimage " +
                $"-c:a aac -b:a 192k -pix_fmt yuv420p -t {GetAudioDuration(finalAudio)} -y \"{tempVideo}\""
            );

            // 4️⃣ Overlay subtitles
            try
            {
                string srtEscaped = srtFile.Replace("\\", "/").Replace(":", "\\:").Replace("'", "\\'");
                RunFFmpeg(
                    $"-i \"{tempVideo}\" -vf " +
                    $"subtitles='{srtEscaped}':force_style=" +
                    $"'FontName=Arial,FontSize=26,PrimaryColour=&H00FFFFFF,OutlineColour=&H00000000," +
                    $"BorderStyle=1,Outline=3,Shadow=0,Alignment=2,MarginV=30' " +
                    $"-c:v libx264 -crf 23 -preset fast -c:a copy -y \"{outputVideo}\""
                );
            }
            catch
            {
                RunFFmpeg(
                    $"-i \"{tempVideo}\" -c:v libx264 -crf 23 -preset fast -c:a copy -y \"{outputVideo}\""
                );
                MessageBox.Show("⚠ Subtitles failed to overlay. Video created without subtitles.");
            }

            // 5️⃣ Cleanup temp files
            TryDeleteFile(tempVideo);
            TryDeleteFile(finalAudio);
            if (scaledImage != imageFile) TryDeleteFile(scaledImage);

            MessageBox.Show($"✅ Video created successfully:\n{outputVideo}\n✅ SRT created:\n{srtFile}");
        }

        // ====== UTILITIES ======

        void GenerateAudioAndSrtByPhrase(string text, string finalAudio, string srtFile)
        {
            char[] separators = new char[] { '.', ',', '?', '!', ';', ':' };
            string[] chunks = text.Split(separators, StringSplitOptions.RemoveEmptyEntries);

            string tempFolder = Path.Combine(Path.GetTempPath(), "tts_chunks");
            Directory.CreateDirectory(tempFolder);

            List<string> chunkFiles = new List<string>();
            List<string> chunkTexts = new List<string>();

            int i = 0;
            foreach (var chunk in chunks)
            {
                string trimmed = chunk.Trim();
                if (trimmed.Length == 0) continue;

                // Restore punctuation if available
                int idx = text.IndexOf(trimmed) + trimmed.Length;
                if (idx < text.Length && Array.Exists(separators, c => c == text[idx]))
                    trimmed += text[idx];

                string chunkWav = Path.Combine(tempFolder, $"chunk_{i}.wav");
                chunkFiles.Add(chunkWav);
                chunkTexts.Add(trimmed);

                // Run TTS for this chunk
                string ttsExe = @"C:\Users\USER\AppData\Local\Programs\Python\Python310\Scripts\tts.exe";
                string args = $"--text \"{trimmed}\" --model_name tts_models/en/ljspeech/tacotron2-DDC --vocoder_name vocoder_models/en/ljspeech/hifigan_v2 --out_path \"{chunkWav}\"";

                Process p = new Process();
                p.StartInfo.FileName = ttsExe;
                p.StartInfo.Arguments = args;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                p.WaitForExit();

                i++;
            }

            // Merge all chunk WAV files into finalAudio
            using (var waveFileWriter = new WaveFileWriter(finalAudio, new WaveFileReader(chunkFiles[0]).WaveFormat))
            {
                foreach (var f in chunkFiles)
                {
                    using var reader = new WaveFileReader(f);
                    reader.CopyTo(waveFileWriter);
                }
            }

            // Generate SRT
            StringBuilder sb = new StringBuilder();
            TimeSpan currentTime = TimeSpan.Zero;
            int index = 1;

            foreach (var f in chunkFiles)
            {
                double duration = GetAudioDuration(f);
                TimeSpan endTime = currentTime + TimeSpan.FromSeconds(duration);

                sb.AppendLine(index.ToString());
                sb.AppendLine($"{FormatTime(currentTime)} --> {FormatTime(endTime)}");
                sb.AppendLine(chunkTexts[index - 1]);
                sb.AppendLine();

                currentTime = endTime;
                index++;
            }

            File.WriteAllText(srtFile, sb.ToString());

            // Cleanup individual chunks
            foreach (var f in chunkFiles)
                TryDeleteFile(f);
        }

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

        string PreScaleImageToExactSize(string img, int targetWidth, int targetHeight)
        {
            string scaled = Path.Combine(Path.GetTempPath(), $"scaled_{Guid.NewGuid():N}.png");
            RunFFmpeg($"-i \"{img}\" -vf \"scale={targetWidth}:{targetHeight},format=yuv420p\" -y \"{scaled}\"");
            return scaled;
        }

        string FormatTime(TimeSpan t) => $"{t:hh\\:mm\\:ss\\,fff}";

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

        void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
