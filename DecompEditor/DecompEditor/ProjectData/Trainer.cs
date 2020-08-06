using DecompEditor.ParserUtils;
using DecompEditor.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DecompEditor {
  /// <summary>
  /// This class represents a specific trainer within the project.
  /// </summary>
  public class Trainer : ObservableObject {
    private string identifier;
    private TrainerClass @class;
    private CDefine encounterMusic;
    private TrainerPic pic;
    private string name;
    private ObservableCollection<Item> items;
    private bool doubleBattle;
    private bool isMale = true;
    private ObservableCollection<CDefine> aiFlags;
    private TrainerParty party;

    /// <summary>
    /// The C identifier of the trainer.
    /// </summary>
    public string Identifier {
      get => identifier;
      set {
        var oldIdentifier = identifier;
        if (!Set(ref identifier, value) || oldIdentifier == null)
          return;
        Project.Instance.registerFileReplacement("TRAINER_" + oldIdentifier,
                                                 "TRAINER_" + identifier);
        if (party != null)
          party.CppVariable = "sParty_" + identifier.fromSnakeToPascal();
      }
    }
    /// <summary>
    /// The class of the trainer.
    /// </summary>
    public TrainerClass Class { get => @class; set => Set(ref @class, value); }
    /// <summary>
    /// The encounter music of the trainer battle.
    /// </summary>
    public CDefine EncounterMusic { get => encounterMusic; set => Set(ref encounterMusic, value); }
    /// <summary>
    /// The front pic of the trainer.
    /// </summary>
    public TrainerPic Pic { get => pic; set => Set(ref pic, value); }
    /// <summary>
    /// The name of the trainer.
    /// </summary>
    public string Name { get => name; set => Set(ref name, value); }
    /// <summary>
    /// The items held by the trainer.
    /// </summary>
    public ObservableCollection<Item> Items {
      get => items;
      set => SetAndTrack(ref items, value);
    }
    /// <summary>
    /// Whether this trainer is a double battle or not.
    /// </summary>
    public bool DoubleBattle { get => doubleBattle; set => Set(ref doubleBattle, value); }
    /// <summary>
    /// Whether this trainer is male or not.
    /// </summary>
    public bool IsMale { get => isMale; set => Set(ref isMale, value); }
    /// <summary>
    /// Whether this trainer female or not.
    /// </summary>
    public bool IsFemale { get => !IsMale; set => IsMale = !value; }
    /// <summary>
    /// The AI flags used by this trainer during battle.
    /// </summary>
    public ObservableCollection<CDefine> AiFlags {
      get => aiFlags;
      set => SetAndTrack(ref aiFlags, value);
    }
    /// <summary>
    /// The party of Pokemon used by this trainer.
    /// </summary>
    public TrainerParty Party { get => party; set => SetAndTrack(ref party, value); }

    public Trainer() {
      AiFlags = new ObservableCollection<CDefine>();
      Items = new ObservableCollection<Item>();
    }
  }

  class TrainerDatabase : DatabaseBase {
    private ObservableCollection<Trainer> trainers;

    /// <summary>
    /// The name of this database.
    /// </summary>
    public override string Name => "Trainer Database";

    /// <summary>
    /// Returns the trainers defined within the project.
    /// </summary>
    public ObservableCollection<Trainer> Trainers {
      get => trainers;
      private set => SetAndTrackItemUpdates(ref trainers, value, this);
    }

    public TrainerDatabase() => Trainers = new ObservableCollection<Trainer>();

    /// <summary>
    /// Reset the data within this database.
    /// </summary>
    protected override void reset() => Trainers.Clear();

    /// <summary>
    /// Deserialize the trainers within the project directory.
    /// </summary>
    /// <param name="deserializer"></param>
    protected override void deserialize(ProjectDeserializer deserializer)
      => Deserializer.deserialize(deserializer, this);

    /// <summary>
    /// Serialize the trainer data to the project directory.
    /// </summary>
    /// <param name="serializer"></param>
    protected override void serialize(ProjectSerializer serializer)
      => Serializer.serialize(serializer, this);

    class Deserializer {
      class TrainerDeserializer : StructDeserializer<Trainer> {
        public TrainerDeserializer(Dictionary<string, TrainerParty> cppToParty, Action<string, Trainer> handler) : base(handler) {
          addEnum("trainerClass", (tclass) => current.Class = Project.Instance.TrainerClasses.getFromId(tclass.Remove(0, "TRAINER_CLASS_".Length)));
          addEnumMask("encounterMusic_gender", (flags) => {
            current.IsMale = flags.Length == 1;
            current.EncounterMusic = Project.Instance.TrainerEncounterMusic.getFromId(flags[^1]);
          });
          addEnum("trainerPic", (pic) => current.Pic = Project.Instance.TrainerPics.getFromID(pic.Remove(0, "TRAINER_PIC_".Length)));
          addString("trainerName", (name) => current.Name = name);
          addEnumList("items", (items) => {
            if (items.Length == 0)
              current.Items = new ObservableCollection<Item>(Enumerable.Repeat(Project.Instance.Items.getFromId("ITEM_NONE"), 4));
            else
              current.Items = new ObservableCollection<Item>(items.Select(item => Project.Instance.Items.getFromId(item)));
          });
          addEnum("doubleBattle", (doubleBattle) => current.DoubleBattle = doubleBattle[0] != 'F');
          addEnumMask("aiFlags", (flags) => {
            if (flags[0][0] != '0') {
              foreach (string flag in flags)
                current.AiFlags.Add(Project.Instance.BattleAI.getFromId(flag));
            }
          });
          addEnum("party", (partyStruct) => {
            int partyNameIndex = partyStruct.LastIndexOf(' ') + 1;
            string partyName = partyStruct.Substring(partyNameIndex, partyStruct.Length - partyNameIndex - 1);
            if (cppToParty.TryGetValue(partyName, out TrainerParty party))
              current.Party = party;
          });
        }
      }
      public static void deserialize(ProjectDeserializer deserializer, TrainerDatabase database) {
        var cppToParty = new Dictionary<string, TrainerParty>();
        loadTrainerParties(deserializer, cppToParty);
        loadTrainers(deserializer, database, cppToParty);
      }
      static void loadTrainerParties(ProjectDeserializer deserializer, Dictionary<string, TrainerParty> cppToParty) {
        var partyDeserializer = new TrainerParty.Deserializer(cppToParty);
        deserializer.deserializeFile(partyDeserializer, "src", "data", "trainer_parties.h");
      }
      static void loadTrainers(ProjectDeserializer deserializer, TrainerDatabase database,
                               Dictionary<string, TrainerParty> cppToParty) {
        var trainerDeserializer = new TrainerDeserializer(cppToParty, (name, trainer) => {
          trainer.Identifier = name.Remove(0, "TRAINER_".Length);
          if (trainer.Identifier != "NONE")
            database.Trainers.Add(trainer);
        });
        var trainerListDeserialzier = new ArrayDeserializer(trainerDeserializer, "gTrainers");
        deserializer.deserializeFile(trainerListDeserialzier, "src", "data", "trainers.h");
      }
    }

    class Serializer {
      public static void serialize(ProjectSerializer serializer, TrainerDatabase database) {
        saveTrainerParties(serializer, database);
        saveTrainers(serializer, database);
      }
      static void saveTrainerParties(ProjectSerializer serializer, TrainerDatabase database) {
        serializer.serializeFile((stream) => {
          foreach (Trainer trainer in database.Trainers)
            TrainerParty.Serializer.serialize(trainer.Party, stream);
        }, "src", "data", "trainer_parties.h");
      }
      static void saveTrainers(ProjectSerializer serializer, TrainerDatabase database) {
        serializer.serializeFile((stream) => {
          stream.WriteLine("const struct Trainer gTrainers[] = {");
          stream.WriteLine(
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
            saveTrainer(stream, trainer);
          stream.WriteLine("};\n");
        }, "src", "data", "trainers.h");
      }

      static void saveTrainer(StreamWriter stream, Trainer trainer) {
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
        stream.WriteLine("TRAINER_ENCOUNTER_MUSIC_" + trainer.EncounterMusic.Identifier + ",");
        stream.WriteLine("        .trainerPic = TRAINER_PIC_" + trainer.Pic.Identifier + ",");
        stream.WriteLine("        .trainerName = _(\"" + trainer.Name + "\"),");

        bool hasItems = trainer.Items.Any(item => item.Identifier != "ITEM_NONE");
        stream.WriteLine("        .items = {" + (hasItems ? string.Join(", ", trainer.Items.Select(item => item.Identifier)) : "") + "},");
        stream.WriteLine("        .doubleBattle = " + (trainer.DoubleBattle ? "TRUE," : "FALSE,"));
        stream.Write("        .aiFlags = ");
        if (trainer.AiFlags.Count == 0)
          stream.WriteLine("0,");
        else
          stream.WriteLine(string.Join(" | ", trainer.AiFlags.OrderBy(flag => flag.Order).Select(flag => "AI_SCRIPT_" + flag.Identifier)) + ",");
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
