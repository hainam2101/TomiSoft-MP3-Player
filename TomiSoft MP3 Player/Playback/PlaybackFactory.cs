﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using Un4seen.Bass;

namespace TomiSoft_MP3_Player {
	class PlaybackFactory {
		private static IPlaybackManager lastInstance;

		public static IPlaybackManager LoadFile(string Filename) {
			string Extension = PlayerUtils.GetFileExtension(Filename);

			if (BassManager.GetSupportedExtensions().Contains(Extension)) {
				lastInstance = new LocalAudioFilePlayback(Filename);
				return lastInstance;
			}

			throw new Exception("Nem támogatott fájlformátum");
		}

		public static IPlaybackManager NullPlayback(int Volume) {
			lastInstance = new NullPlayback() {
				Volume = Volume
			};

			return lastInstance;
		}

		public static IPlaybackManager GetLastInstance() {
			return lastInstance;
		}
	}
}
