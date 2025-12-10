using System;
using System.Diagnostics;
using System.IO;
using System.Text;
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

            string tempWav = Path.Combine(Path.GetTempPath(), "audio.wav");
            string tempMp3 = Path.Combine(Path.GetTempPath(), "audio.mp3");
            string tempVideo = Path.Combine(Path.GetTempPath(), "tempVideo.mp4");

            // 1️⃣ Generate TTS audio
            GenerateHumanAudio(text, tempWav);

            // 2️⃣ Convert WAV to MP3 for FFmpeg
            RunFFmpeg($"-i \"{tempWav}\" -c:a libmp3lame -q:a 4 -y \"{tempMp3}\"");

            // 3️⃣ Generate dynamic SRT
            GenerateSrtFromText(text, srtFile, GetAudioDuration(tempMp3));

            int videoWidth = 1920;
            int videoHeight = 1080;

            // 4️⃣ Resize image if needed
            string scaledImage = imageFile;
            if (!IsImageSizeMatch(imageFile, videoWidth, videoHeight))
            {
                MessageBox.Show($"Input image must be {videoWidth}x{videoHeight}. Resizing automatically.");
                scaledImage = PreScaleImageToExactSize(imageFile, videoWidth, videoHeight);
            }

            // 5️⃣ Create video with audio
            RunFFmpeg(
                $"-loop 1 -i \"{scaledImage}\" -i \"{tempMp3}\" " +
                $"-c:v libx264 -preset fast -crf 23 -tune stillimage " +
                $"-c:a aac -b:a 192k -pix_fmt yuv420p -t {GetAudioDuration(tempMp3)} -y \"{tempVideo}\""
            );

            // 6️⃣ Overlay subtitles
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

            // 7️⃣ Cleanup temp files
            TryDeleteFile(tempVideo);
            TryDeleteFile(tempMp3);
            TryDeleteFile(tempWav);
            if (scaledImage != imageFile) TryDeleteFile(scaledImage);

            MessageBox.Show($"✅ Video created successfully:\n{outputVideo}\n✅ SRT created:\n{srtFile}");
        }

        // ====== UTILITIES ======

        void GenerateHumanAudio(string text, string outputWav)
        {
            string ttsExe = @"C:\Users\USER\AppData\Local\Programs\Python\Python310\Scripts\tts.exe";
            string args = $"--text \"{text}\" --model_name tts_models/en/ljspeech/tacotron2-DDC --vocoder_name vocoder_models/en/ljspeech/hifigan_v2 --out_path \"{outputWav}\"";

            Process p = new Process();
            p.StartInfo.FileName = ttsExe;
            p.StartInfo.Arguments = args;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.CreateNoWindow = true;
            p.Start();

            string output = p.StandardOutput.ReadToEnd();
            string error = p.StandardError.ReadToEnd();
            p.WaitForExit();

            if (p.ExitCode != 0)
                throw new Exception("TTS failed: " + error);
        }

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
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }

        // ====== DYNAMIC WORD-LEVEL SUBTITLES ======
        void GenerateSrtFromText(string text, string srtFile, double audioDuration)
        {
            StringBuilder sb = new StringBuilder();

            // Split text into sentences using punctuation.
            string[] sentences = text
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Split(new[] { '.', '?', '!' }, StringSplitOptions.RemoveEmptyEntries);

            // Add punctuation back (because Split removes it)
            List<string> finalSentences = new List<string>();
            foreach (string s in sentences)
            {
                string trimmed = s.Trim();
                if (trimmed.Length == 0) continue;

                // Detect punctuation from original text
                int index = text.IndexOf(trimmed) + trimmed.Length;
                char end = '.';
                if (index < text.Length)
                {
                    if (text[index] == '.' || text[index] == '?' || text[index] == '!')
                        end = text[index];
                }

                finalSentences.Add(trimmed + end);
            }

            // Calculate total sentence count
            int total = finalSentences.Count;
            double secPerSentence = audioDuration / total;

            TimeSpan currentTime = TimeSpan.Zero;
            int srtIndex = 1;

            // Create SRT timing block per sentence
            foreach (string sentence in finalSentences)
            {
                TimeSpan endTime = currentTime + TimeSpan.FromSeconds(secPerSentence);

                sb.AppendLine(srtIndex.ToString());
                sb.AppendLine($"{FormatTime(currentTime)} --> {FormatTime(endTime)}");
                sb.AppendLine(sentence);
                sb.AppendLine();

                currentTime = endTime;
                srtIndex++;
            }

            File.WriteAllText(srtFile, sb.ToString());
        }
    }
}
