using DecompEditor.ParserUtils;
using System.Collections.Generic;
using Truncon.Collections;

namespace DecompEditor {
  class TrainerEncounterMusicDatabase : DatabaseBase {
    OrderedDictionary<string, CDefine> encounterMusic = new OrderedDictionary<string, CDefine>();

    public IEnumerable<CDefine> EncounterMusic => encounterMusic.Values;
    public CDefine getFromId(string id) => encounterMusic[id];

    protected override void reset() => encounterMusic.Clear();

    protected override void deserialize(ProjectDeserializer serializer) {
      encounterMusic = serializer.parseDefineNames(
        "TRAINER_ENCOUNTER_MUSIC_", "include", "constants", "trainers.h");
    }
  }
}
