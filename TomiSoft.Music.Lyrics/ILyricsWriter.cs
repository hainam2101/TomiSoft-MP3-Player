﻿using System.Collections.Generic;

namespace TomiSoft.Music.Lyrics {
	/// <summary>
	/// Represents a lyrics writer.
	/// </summary>
	public interface ILyricsWriter {
		/// <summary>
		/// Gets if the used file format supports multiple translations.
		/// </summary>
		bool SupportsMultipleTranslations { get; }

		/// <summary>
		/// Gets or sets the album of the song.
		/// </summary>
		string Album { get; set; }

		/// <summary>
		/// Gets or sets the artist of the song.
		/// </summary>
		string Artist { get; set; }

		/// <summary>
		/// Gets or sets the ID of the default translation.
		/// </summary>
		string DefaultTranslationID { get; set; }

		/// <summary>
		/// Gets or sets the title of the song.
		/// </summary>
		string Title { get; set; }

		/// <summary>
		/// Gets the translations supported by the lyrics file.
		/// </summary>
		IReadOnlyDictionary<string, string> Translations { get; }

		/// <summary>
		/// Adds a new translation to the lyrics file.
		/// </summary>
		/// <param name="Language">The language of the translation</param>
		void AddTranslation(string Language);

		/// <summary>
		/// Adds a new line to the lyrics file.
		/// </summary>
		/// <param name="TranslationID">The translation ID that this line applies to</param>
		/// <param name="StartTime">The start time of the lyrics line in seconds (when to begin displaying the line)</param>
		/// <param name="EndTime">The end time of the lyrics line in seconds (when to hide the line)</param>
		/// <param name="Text">The text of the line</param>
		void AddLine(string TranslationID, double StartTime, double EndTime, string Text);

		/// <summary>
		/// Adds a new line to the lyrics file.
		/// </summary>
		/// <param name="TranslationID">The translation ID that this line applies to</param>
		/// <param name="Line">The ILyricsLine instance that holds the data of the lyrics line</param>
		void AddLine(string TranslationID, ILyricsLine Line);

		/// <summary>
		/// Constructs the lyrics file.
		/// </summary>
		/// <returns>The contents of the lyrics file.</returns>
		string Build();
	}
}