﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TomiSoft.MP3Player.Playback.YouTube {
	/// <summary>
	/// Represents the audio format for youtube-dl --audio-format argument.
	/// </summary>
	enum YoutubeDlAudioFormat {
		aac, flac, mp3, m4a, opus, vorbis
	}

	/// <summary>
	/// A wrapper class for youtube-dl that can be used to download media from YouTube.
	/// </summary>
	class YoutubeDl {
		/// <summary>
		/// Stores the relative path of the youtube-dl.exe file according to the
		/// <see cref="WorkingDirectory"/>.
		/// </summary>
		private readonly string ExecutablePath;

		/// <summary>
		/// Stores the working directory for executing youtube-dl.
		/// </summary>
		private readonly string WorkingDirectory;

		/// <summary>
		/// This event occures when a download has failed because a software
		/// update is needed.
		/// </summary>
		public event EventHandler UpdateRequired;

		/// <summary>
		/// Gets a YouTube watch link generated from <see cref="VideoID"/>.
		/// </summary>
		public string YoutubeUri {
			get {
				return $"https://youtube.com/watch?v={this.VideoID}";
			}
		}

		/// <summary>
		/// Gets of sets the YouTube video ID.
		/// </summary>
		public string VideoID {
			get;
			set;
		}

		/// <summary>
		/// Gets or sets the path of the downloaded file.
		/// </summary>
		public string Filename {
			get;
			set;
		}

		/// <summary>
		/// Gets or sets the format of the downloaded audio file. Default is
		/// <see cref="YoutubeDlAudioFormat.mp3"/>.
		/// </summary>
		public YoutubeDlAudioFormat AudioFileFormat {
			get;
			set;
		} = YoutubeDlAudioFormat.mp3;

		/// <summary>
		/// Initializes a new instance of the <see cref="YoutubeDl"/> class.
		/// </summary>
		/// <param name="ExecutablePathInWorkingDirectory">The relative path of the youtube-dl.exe file according to the <paramref name="WorkingDirectory"/></param>
		/// <param name="WorkingDirectory">The working directory for executing youtube-dl.</param>
		/// <exception cref="DirectoryNotFoundException">when <paramref name="WorkingDirectory"/> not found</exception>
		/// <exception cref="FileNotFoundException">when <paramref name="ExecutablePathInWorkingDirectory"/> not found in <paramref name="WorkingDirectory"/></exception>
		public YoutubeDl(string ExecutablePathInWorkingDirectory, string WorkingDirectory) {
			#region Error checking
			if (!Directory.Exists(WorkingDirectory))
				throw new DirectoryNotFoundException($"Directory does not exists: {WorkingDirectory}");

			if (!File.Exists(Path.Combine(WorkingDirectory, ExecutablePathInWorkingDirectory)))
				throw new FileNotFoundException($"youtube-dl was not found at path: {ExecutablePathInWorkingDirectory}");
			#endregion

			this.ExecutablePath = ExecutablePathInWorkingDirectory;
			this.WorkingDirectory = WorkingDirectory;
		}

		/// <summary>
		/// Downloads the audio media asynchronously given by its ID in <see cref="VideoID"/> to the path specified in 
		/// <see cref="Filename"/> with the specified audio format given in <see cref="AudioFileFormat"/>.
		/// </summary>
		/// <param name="Progress">An <see cref="IProgress{YoutubeDownloadProgress}"/> instance that can be used to report the progress of the download. Can be null.</param>
		/// <returns>A <see cref="Task"/> that represents the asynchronous process.</returns>
		public async Task DownloadAudioAsync(IProgress<YoutubeDownloadProgress> Progress) {
			string TempPath = Path.ChangeExtension(this.Filename, ".tmp");
			if (File.Exists(TempPath))
				File.Delete(TempPath);

			Process DownloaderProcess = new Process { StartInfo = this.GetProcessStartInfo(TempPath) };

			DownloaderProcess.OutputDataReceived += (o, e) => ReportProgress(e.Data, Progress);
			DownloaderProcess.ErrorDataReceived += (o, e) => {
				if (!String.IsNullOrWhiteSpace(e.Data)) {
					Progress?.Report(new YoutubeDownloadProgress(YoutubeDownloadStatus.Error, 0));
					Trace.TraceWarning($"youtube-dl StdErr: {e.Data}");

					DownloaderProcess.Kill();

					this.CheckIfUpdateRequired(e.Data);
				}
			};

			Progress?.Report(new YoutubeDownloadProgress(YoutubeDownloadStatus.Initializing, 0));

			DownloaderProcess.Start();
			DownloaderProcess.BeginOutputReadLine();
			DownloaderProcess.BeginErrorReadLine();

			await Task.Run(() => DownloaderProcess.WaitForExit());

			Progress?.Report(new YoutubeDownloadProgress(YoutubeDownloadStatus.Completed, 100));
		}
		
		/// <summary>
		/// Downloads informations about the video asynchronously.
		/// </summary>
		/// <returns>An <see cref="ExpandoObject"/> instance that holds the video information.</returns>
		public async Task<dynamic> GetVideoInfo() {
			Process DownloaderProcess = new Process() {
				StartInfo = new ProcessStartInfo {
					FileName = this.ExecutablePath,
					Arguments = $"--quiet --print-json {this.YoutubeUri}",
					UseShellExecute = false,
					WindowStyle = ProcessWindowStyle.Hidden,
					CreateNoWindow = true,
					WorkingDirectory = this.WorkingDirectory,
					RedirectStandardOutput = true,
					RedirectStandardError = true
				}
			};

			StringBuilder StdOut = new StringBuilder();
			DownloaderProcess.OutputDataReceived += (o, e) => StdOut.Append(e.Data);

			DownloaderProcess.Start();
			DownloaderProcess.BeginOutputReadLine();

			await Task.Run(() => DownloaderProcess.WaitForExit());

			string json = StdOut.ToString();
			dynamic Result = JsonConvert.DeserializeObject<ExpandoObject>(json, new ExpandoObjectConverter());

			return Result;
		}

		/// <summary>
		/// Updates youtube-dl asynchronously.
		/// </summary>
		/// <returns>A <see cref="Task"/> that represents the asynchronous process.</returns>
		public async Task UpdateAsync() {
			Process DownloaderProcess = new Process() {
				StartInfo = new ProcessStartInfo {
					FileName = this.ExecutablePath,
					Arguments = "-U",
					UseShellExecute = true,
					WindowStyle = ProcessWindowStyle.Hidden,
					WorkingDirectory = this.WorkingDirectory
				}
			};

			DownloaderProcess.Start();
			await Task.Run(() => DownloaderProcess.WaitForExit());
		}

		private Task WaitForExitAsync(Process p) {
			return Task.Run(() => p.WaitForExit());
		}

		private void CheckIfUpdateRequired(string ErrorLine) {
			#region Error checking
			if (String.IsNullOrWhiteSpace(ErrorLine))
				return;
			#endregion

			if (ErrorLine.Contains("Make sure you are using the latest version"))
				this.UpdateRequired?.Invoke(this, EventArgs.Empty);
		}
		
		/// <summary>
		/// Extracts the status information from the standard output of the youtube-dl.
		/// </summary>
		/// <param name="Output">The current output line of youtube-dl.</param>
		/// <param name="Progress">An <see cref="IProgress{YoutubeDownloadProgress}"/> instance that can be used to report the progress of the download. Can be null.</param>
		private static void ReportProgress(string Output, IProgress<YoutubeDownloadProgress> Progress) {
			#region Error checking
			if (Progress == null || String.IsNullOrWhiteSpace(Output))
				return;
			#endregion

			Trace.TraceInformation($"youtube-dl StdOut: {Output}");

			if (Output.ToLower().StartsWith("[download]") && !Output.ToLower().Contains("destination")) {
				string Match = Regex.Match(Output, @"\b\d+([\.,]\d+)?").Value;
				
				double Percentage;
				if (double.TryParse(Match, NumberStyles.Any, CultureInfo.InvariantCulture, out Percentage))
					Progress.Report(new YoutubeDownloadProgress(YoutubeDownloadStatus.Downloading, Percentage));
			}

			else if (Output.ToLower().StartsWith("[ffmpeg]")) {
				Progress.Report(new YoutubeDownloadProgress(YoutubeDownloadStatus.Converting, 0));
			}
		}

		/// <summary>
		/// Gets the <see cref="ProcessStartInfo"/> instance for the youtube-dl download process.
		/// </summary>
		/// <param name="TempFilename">A temporary file that will hold the unprocessed media.</param>
		/// <returns>The <see cref="ProcessStartInfo"/> instance.</returns>
		private ProcessStartInfo GetProcessStartInfo(string TempFilename) {
			return new ProcessStartInfo {
				FileName = this.ExecutablePath,
				Arguments = $"--extract-audio --audio-format {this.AudioFileFormat} -o \"{TempFilename}\" {this.YoutubeUri}",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				WindowStyle = ProcessWindowStyle.Hidden,
				CreateNoWindow = true,
				WorkingDirectory = this.WorkingDirectory
			};
		}
	}
}
