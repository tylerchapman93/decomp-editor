using DecompEditor.Utils;
using System.Collections.Generic;
using System.IO;

namespace DecompEditor {
  class TrainerEncounterMusicDatabase {
    readonly List<string> encounterMusic = new List<string>();

    public IEnumerable<string> EncounterMusic => encounterMusic;

    public void reset() => encounterMusic.Clear();

    public void load(string projectDir) {
      reset();

      StreamReader reader = File.OpenText(Path.Combine(projectDir, "include", "constants", "trainers.h"));
      while (!reader.EndOfStream) {
        if (reader.ReadLine().tryExtractPrefix("#define TRAINER_ENCOUNTER_MUSIC_", " ", out string musicName))
          encounterMusic.Add(musicName);
      }
    }
  }
}
