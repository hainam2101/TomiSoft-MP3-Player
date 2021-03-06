﻿using System.Collections.Generic;

namespace TomiSoft.ExternalApis.YoutubeDl {
	public class YoutubeDownloadProgress {
		public YoutubeDownloadStatus Status {
			get;
			private set;
		}

		public double Percentage {
			get;
			private set;
		}

		public YoutubeDownloadProgress(YoutubeDownloadStatus Status, double Percentage) {
			this.Status = Status;
			this.Percentage = Percentage;
		}

		public override string ToString() {
			Dictionary<YoutubeDownloadStatus, string> Dict = new Dictionary<YoutubeDownloadStatus, string>() {
				{ YoutubeDownloadStatus.Updating, "youtube-dl frissítése..." },
				{ YoutubeDownloadStatus.Initializing, "Letöltés indítása..." },
				{ YoutubeDownloadStatus.Converting, "Videó átalakítása MP3 fájllá..." },
				{ YoutubeDownloadStatus.Completed, "Letöltés kész" },
				{ YoutubeDownloadStatus.Error, "Nem sikerült letölteni a zenét." },
			};

			if (this.Status == YoutubeDownloadStatus.Downloading) {
				return $"{this.Percentage}% letöltve";
			}
			else {
				return Dict[this.Status];
			}
		}
	}
}
