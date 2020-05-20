using DecompEditor.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace DecompEditor {
  public class Trainer : ObservableObject {
    private string identifier;
    private TrainerClass @class;
    private string encounterMusic;
    private TrainerPic pic;
    private string name;
    private ObservableCollection<Item> items;
    private bool doubleBattle;
    private bool isMale = true;
    private ObservableCollection<string> aiFlags;
    private TrainerParty party;

    public string Identifier {
      get => identifier;
      set {
        if (identifier != null) {
          Project.Instance.registerFileReplacement("TRAINER_" + identifier,
                                                   "TRAINER_" + value);
        }
        Set(ref identifier, value);
      }
    }
    public TrainerClass Class { get => @class; set => Set(ref @class, value); }
    public string EncounterMusic { get => encounterMusic; set => Set(ref encounterMusic, value); }
    public TrainerPic Pic { get => pic; set => Set(ref pic, value); }
    public string Name { get => name; set => Set(ref name, value); }
    public ObservableCollection<Item> Items {
      get => items;
      set {
        items = value;
        items.trackItemPropertyUpdates(this);
      }
    }
    public bool DoubleBattle { get => doubleBattle; set => Set(ref doubleBattle, value); }
    public bool IsMale {
      get => isMale;
      set => Set(ref isMale, value); 
    }
    public bool IsFemale { get => !IsMale; set => IsMale = !value; }
    public ObservableCollection<string> AiFlags {
      get => aiFlags;
      set {
        Set(ref aiFlags, value);
        aiFlags.trackItemPropertyUpdates(this);
      }
    }
    public TrainerParty Party { get => party; set => Set(ref party, value); }

    public Trainer() {
      AiFlags = new ObservableCollection<string>();
      Items = new ObservableCollection<Item>();
    }
  }

  class TrainerDatabase : ObservableObject {
    public bool IsDirty { 
      get;
      private set;
    }
    public ObservableCollection<Trainer> Trainers { get; } = new ObservableCollection<Trainer>();

    public TrainerDatabase() {
      Trainers.trackItemPropertyUpdates(this, "Trainers");
      PropertyChanged += (sender, e) => IsDirty = true;
    }

    public void reset() {
      Trainers.Clear();
      IsDirty = false;
    }

    public void save(string projectDir, BattleAIDatabase battleAI) {
      if (IsDirty) {
        Serializer.serialize(projectDir, this, battleAI);
        IsDirty = false;
      }
    }

    public void load(string projectDir, BattleAIDatabase battleAI,
                     ItemDatabase itemDatabase, MoveDatabase moveDatabase,
                     PokemonSpeciesDatabase speciesDatabase,
                     TrainerClassDatabase trainerClassDatabase,
                     TrainerPicDatabase trainerPicDatabase) {
      reset();
      Deserializer.deserialize(projectDir, this, battleAI, itemDatabase, moveDatabase, speciesDatabase,
                               trainerClassDatabase, trainerPicDatabase);
      IsDirty = false;
    }

    class Deserializer {
      class TrainerStruct : CParser.Struct {
        public Trainer currentTrainer;
        public BattleAIDatabase battleAI;
        public ItemDatabase itemDatabase;
        public TrainerClassDatabase trainerClassDatabase;
        public TrainerPicDatabase trainerPicDatabase;
        public Dictionary<string, TrainerParty> cppToParty;

        public TrainerStruct() {
          addEnum("trainerClass", (tclass) => currentTrainer.Class = trainerClassDatabase.getClassFromId(tclass.Remove(0, "TRAINER_CLASS_".Length)));
          addEnumMask("encounterMusic_gender", (flags) => {
            currentTrainer.IsMale = flags.Length == 1;
            currentTrainer.EncounterMusic = flags[^1].Remove(0, "TRAINER_ENCOUNTER_MUSIC_".Length);
          });
          addEnum("trainerPic", (pic) => currentTrainer.Pic = trainerPicDatabase.getFromID(pic.Remove(0, "TRAINER_PIC_".Length)));
          addString("trainerName", (name) => currentTrainer.Name = name);
          addEnumList("items", (items) => {
            if (items.Length == 0)
              currentTrainer.Items = new ObservableCollection<Item>(Enumerable.Repeat(itemDatabase.getFromId("ITEM_NONE"), 4));
            else
              currentTrainer.Items = new ObservableCollection<Item>(items.Select(item => itemDatabase.getFromId(item)));
          });
          addEnum("doubleBattle", (doubleBattle) => currentTrainer.DoubleBattle = doubleBattle[0] != 'F');
          addEnumMask("aiFlags", (flags) => {
            if (flags[0][0] != '0') {
              foreach (string flag in flags)
                currentTrainer.AiFlags.Add(battleAI.getNameFromEnum(flag));
            }
          });
          addEnum("party", (partyStruct) => {
            int partyNameIndex = partyStruct.LastIndexOf(' ') + 1;
            string partyName = partyStruct.Substring(partyNameIndex, partyStruct.Length - partyNameIndex - 1);
            if (!cppToParty.TryGetValue(partyName, out TrainerParty party))
              throw new Exception("unknown trainer party");
            currentTrainer.Party = party;
          });
        }
      }
      static readonly TrainerStruct trainerSerializer = new TrainerStruct();

      public static void deserialize(string projectDir, TrainerDatabase database, BattleAIDatabase battleAI,
                                     ItemDatabase itemDatabase, MoveDatabase moveDatabase,
                                     PokemonSpeciesDatabase speciesDatabase,
                                     TrainerClassDatabase trainerClassDatabase,
                                     TrainerPicDatabase trainerPicDatabase) {
        var cppToParty = new Dictionary<string, TrainerParty>();
        loadTrainerParties(projectDir, itemDatabase, moveDatabase, speciesDatabase, cppToParty);
        loadTrainers(projectDir, database, cppToParty, battleAI, itemDatabase, trainerClassDatabase,
                     trainerPicDatabase);
      }

      static void loadTrainerParties(string projectDir, ItemDatabase itemDatabase,
                                     MoveDatabase moveDatabase, PokemonSpeciesDatabase speciesDatabase,
                                     Dictionary<string, TrainerParty> cppToParty) {
        StreamReader reader = File.OpenText(Path.Combine(projectDir, "src", "data", "trainer_parties.h"));
        while (!reader.EndOfStream) {
          TrainerParty party = TrainerParty.Deserializer.deserialize(
              reader, itemDatabase, moveDatabase, speciesDatabase);
          cppToParty[party.CppVariable] = party;
        }
        reader.Close();
      }

      static void loadTrainers(string projectDir, TrainerDatabase database, Dictionary<string, TrainerParty> cppToParty,
                               BattleAIDatabase battleAI, ItemDatabase itemDatabase,
                               TrainerClassDatabase trainerClassDatabase,
                               TrainerPicDatabase trainerPicDatabase) {
        StreamReader reader = File.OpenText(Path.Combine(projectDir, "src", "data", "trainers.h"));
        reader.ReadLine();

        // Skip the first trainer as it is TRAINER_NONE.
        while (!reader.EndOfStream && reader.ReadLine() != "")
          continue;
        while (!reader.EndOfStream) {
          if (reader.Peek() == '}')
            break;
          string defLine = reader.ReadLine();
          int skipLen = "    [TRAINER_".Length;

          var trainer = new Trainer {
            Identifier = defLine.Substring(skipLen, defLine.Length - skipLen - 3)
          };
          reader.ReadLine();

          trainerSerializer.cppToParty = cppToParty;
          trainerSerializer.battleAI = battleAI;
          trainerSerializer.itemDatabase = itemDatabase;
          trainerSerializer.trainerClassDatabase = trainerClassDatabase;
          trainerSerializer.trainerPicDatabase = trainerPicDatabase;
          trainerSerializer.currentTrainer = trainer;
          trainerSerializer.deserialize(reader);
          database.Trainers.Add(trainer);
          reader.ReadLine();
        }
        reader.Close();
      }

    }

    class Serializer {
      public static void serialize(string projectDir, TrainerDatabase database, BattleAIDatabase battleAI) {
        saveTrainerParties(projectDir, database);
        saveTrainers(projectDir, database, battleAI);
      }
      static void saveTrainerParties(string projectDir, TrainerDatabase database) {
        var stream = new StreamWriter(Path.Combine(projectDir, "src", "data", "trainer_parties.h"), false);
        foreach (Trainer trainer in database.Trainers)
          TrainerParty.Serializer.serialize(trainer.Party, stream);
        stream.Close();
      }
      static void saveTrainers(string projectDir, TrainerDatabase database, BattleAIDatabase battleAI) {
        var writer = new StreamWriter(Path.Combine(projectDir, "src", "data", "trainers.h"), false);
        writer.WriteLine("const struct Trainer gTrainers[] = {");
        writer.WriteLine(
@"    [TRAINER_NONE] =
    {
        .partyFlags = 0,
        .trainerClass = TRAINER_CLASS_PKMN_TRAINER_1,
        .encounterMusic_gender = TRAINER_ENCOUNTER_MUSIC_MALE,
        .trainerPic = TRAINER_PIC_HIKER,
        .trainerName = _(""""),
        .items = {},
        .doubleBattle = FALSE,
        .aiFlags = 0,
        .partySize = 0,
        .party = {.NoItemDefaultMoves = NULL},
    },
");
        foreach (Trainer trainer in database.Trainers)
          saveTrainer(writer, trainer, battleAI);

        writer.WriteLine("};\n");
        writer.Close();
      }

      static void saveTrainer(StreamWriter stream, Trainer trainer, BattleAIDatabase battleAI) {
        stream.WriteLine("    [TRAINER_" + trainer.Identifier + "] =");
        stream.WriteLine("    {");
        stream.Write("        .partyFlags = ");
        bool partyHasItems = trainer.Party.HasItems;
        bool partyHasMoves = trainer.Party.HasMoves;
        if (partyHasItems) {
          stream.Write("F_TRAINER_PARTY_HELD_ITEM");
          if (partyHasMoves)
            stream.Write(" | F_TRAINER_PARTY_CUSTOM_MOVESET");
        } else if (partyHasMoves) {
          stream.Write("F_TRAINER_PARTY_CUSTOM_MOVESET");
        } else {
          stream.Write("0");
        }
        stream.WriteLine(",");

        stream.WriteLine("        .trainerClass = TRAINER_CLASS_" + trainer.Class.Identifier + ",");
        stream.Write("        .encounterMusic_gender = ");
        if (!trainer.IsMale)
          stream.Write("F_TRAINER_FEMALE | ");
        stream.WriteLine("TRAINER_ENCOUNTER_MUSIC_" + trainer.EncounterMusic + ",");
        stream.WriteLine("        .trainerPic = TRAINER_PIC_" + trainer.Pic.Identifier + ",");
        stream.WriteLine("        .trainerName = _(\"" + trainer.Name + "\"),");

        bool hasItems = trainer.Items.Any(item => item.Identifier != "ITEM_NONE");
        stream.WriteLine("        .items = {" + (hasItems ? string.Join(", ", trainer.Items.Select(item => item.Identifier)) : "") + "},");
        stream.WriteLine("        .doubleBattle = " + (trainer.DoubleBattle ? "TRUE," : "FALSE,"));
        stream.Write("        .aiFlags = ");
        if (trainer.AiFlags.Count == 0)
          stream.WriteLine("0,");
        else
          stream.WriteLine(string.Join(" | ", battleAI.getEnumsFromNames(trainer.AiFlags)) + ",");
        stream.WriteLine("        .partySize = ARRAY_COUNT(" + trainer.Party.CppVariable + "),");
        stream.Write("        .party = {.");
        if (partyHasItems) {
          stream.Write(partyHasMoves ? "ItemCustomMoves" : "ItemDefaultMoves");
        } else if (partyHasMoves) {
          stream.Write("NoItemCustomMoves");
        } else {
          stream.Write("NoItemDefaultMoves");
        }
        stream.WriteLine(" = " + trainer.Party.CppVariable + "},");
        stream.WriteLine("    },\n");
      }
    }
  }
}
