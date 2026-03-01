using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace RealESRGAN_AI_Upscale {

    /// <summary>
    /// https://github.com/xinntao/Real-ESRGAN
    /// https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan
    /// </summary>
    public class EsrganNcnn {

        private static readonly string EXEC_NAME = "realesrgan-ncnn-vulkan.exe"; // https://github.com/xinntao/Real-ESRGAN-ncnn-vulkan
        private static readonly string BIN_FOLDER = Path.Combine("RealESRGAN");

        /// <summary>
        /// Exec external process for upscaling
        /// </summary>
        /// <param name="in_path"></param>
        /// <param name="out_path"></param>
        /// <param name="upscaleRatio">The upscale ratio (can be 2, 3, 4. default=4)</param>
        /// <returns></returns>
        public static async Task Run(string in_pathFolder, string out_pathFolder, int upscaleRatio) {
            string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, BIN_FOLDER);

            if (!File.Exists(Path.Combine(folderPath, EXEC_NAME))) {
                return;
            }

            string cmd = $"/C cd /D {folderPath} & {EXEC_NAME} -i \"{in_pathFolder}\" -o \"{out_pathFolder}\" -s {upscaleRatio} -m \"models\"";

            Console.WriteLine($"[CMD] {cmd}");

            using (Process proc = new Process()) {
                proc.StartInfo.FileName = "cmd.exe";
                proc.StartInfo.Arguments = cmd;
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.CreateNoWindow = true;

                proc.OutputDataReceived += (sender, outLine) => { 
                    OutputHandler(outLine.Data, false); 
                };
                proc.ErrorDataReceived += (sender, outLine) => { 
                    OutputHandler(outLine.Data, true); 
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                while (!proc.HasExited)
                    await Task.Delay(50);
            }
        }

        private static void OutputHandler(string line, bool error) {
            if (string.IsNullOrWhiteSpace(line) || line.Length < 6)
                return;

            Console.WriteLine($"[NCNN] {line.Replace("\n", " ").Replace("\r", " ")}");
        }
    }
}