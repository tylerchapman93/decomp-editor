using DecompEditor.ParserUtils;
using DecompEditor.Utils;
using System;
using System.Collections.Generic;

namespace DecompEditor {
  public class TrainerClass : ObservableObject {
    private string identifier;
    private string name;
    private int moneyFactor = 0;

    public string Identifier {
      get => identifier;
      set {
        if (identifier != null) {
          Project.Instance.registerFileReplacement("TRAINER_CLASS_" + identifier,
                                                   "TRAINER_CLASS_" + value);
        }
        Set(ref identifier, value);
      }
    }
    public string Name { get => name; set => Set(ref name, value); }
    public int MoneyFactor { get => moneyFactor; set => Set(ref moneyFactor, value); }
  }

  public class TrainerClassDatabase : DatabaseBase {
    readonly Dictionary<string, TrainerClass> idToTrainerClass = new Dictionary<string, TrainerClass>();
    private ObservableCollection<TrainerClass> classes;

    // TODO: These could be configurable.
    public int MaxClassCount => 255;
    public int MaxClassNameLen => 13;

    public ObservableCollection<TrainerClass> Classes {
      get => classes;
      private set => SetAndTrackItemUpdates(ref classes, value, this);
    }
    public TrainerClassDatabase() => Classes = new ObservableCollection<TrainerClass>();

    protected override void reset() {
      Classes.Clear();
      idToTrainerClass.Clear();
    }

    /// Only used during serialization.
    internal TrainerClass getFromId(string id) => idToTrainerClass[id];
    public void addClass(TrainerClass newClass) {
      idToTrainerClass.Add(newClass.Identifier, newClass);
      Classes.Add(newClass);
    }

    protected override void deserialize(ProjectDeserializer deserializer) => Deserializer.deserialize(deserializer, this);
    protected override void serialize(ProjectSerializer serializer) => Serializer.serialize(serializer, this);

    class Deserializer {
      public static void deserialize(ProjectDeserializer deserializer, TrainerClassDatabase database) {
        loadClassNames(deserializer, database);
        loadMoneyFactors(deserializer, database);
      }

      static void loadClassNames(ProjectDeserializer deserializer, TrainerClassDatabase database) {
        deserializer.deserializeFile((reader) => {
          if (!ParserUtils.StructBodyDeserializer.Element.tryDeserializeBracketString(reader.ReadLine(), out string classEnum, out string className))
            return;
          database.addClass(new TrainerClass() {
            Identifier = classEnum.Remove(0, "TRAINER_CLASS_".Length),
            Name = className
          });
        }, "src", "data", "text", "trainer_class_names.h");
      }
      static void loadMoneyFactors(ProjectDeserializer deserializer, TrainerClassDatabase database) {
        var moneyFactorDeserializer = new InlineStructDeserializer((elements) => {
          if (elements[0] == "0xFF")
            return;
          string id = elements[0].Remove(0, "TRAINER_CLASS_".Length);
          if (database.idToTrainerClass.TryGetValue(id, out TrainerClass tClass))
            tClass.MoneyFactor = int.Parse(elements[1]);
        });
        var arrayDeserializer = new ArrayDeserializer(moneyFactorDeserializer, "gTrainerMoneyTable");
        deserializer.deserializeFile(arrayDeserializer, "src", "battle_main.c");
      }
    }
    class Serializer {
      public static void serialize(ProjectSerializer serializer, TrainerClassDatabase database) {
        saveClassIDs(serializer, database);
        saveClassNames(serializer, database);
        saveMoneyFactors(serializer, database);
      }
      static void saveClassIDs(ProjectSerializer serializer, TrainerClassDatabase database) {
        serializer.serializePartialFile(str => str.StartsWith("#define TRAINER_CLASS_"), (stream) => {
          int longestClassID = 4;
          foreach (TrainerClass tClass in database.Classes)
            longestClassID = Math.Max(longestClassID, tClass.Identifier.Length);

          int classCount = 0;
          foreach (TrainerClass tClass in database.Classes) {
            stream.WriteLine(string.Format("#define TRAINER_CLASS_" + "{0}".PadRight(longestClassID - tClass.Identifier.Length) + " 0x{1:X}",
                                           tClass.Identifier, classCount++));
          }
          stream.WriteLine();
        }, "include", "constants", "trainers.h");
      }
      static void saveClassNames(ProjectSerializer serializer, TrainerClassDatabase database) {
        serializer.serializeFile((stream) => {
          stream.WriteLine("const u8 gTrainerClassNames[][13] = {");
          foreach (TrainerClass tClass in database.Classes)
            stream.WriteLine(string.Format("    [TRAINER_CLASS_{0}] = _(\"{1}\"),", tClass.Identifier, tClass.Name));
          stream.WriteLine("};");
        }, "src", "data", "text", "trainer_class_names.h");
      }
      static void saveMoneyFactors(ProjectSerializer serializer, TrainerClassDatabase database) {
        serializer.serializePartialFile(str => str.EndsWith("gTrainerMoneyTable[] ="), str => str.StartsWith("}"), (stream) => {
          stream.WriteLine("const struct TrainerMoney gTrainerMoneyTable[] =");
          stream.WriteLine("{");
          foreach (TrainerClass tClass in database.Classes) {
            if (tClass.MoneyFactor != 0)
              stream.WriteLine(string.Format("    {0}TRAINER_CLASS_{1}, {2}{3},", "{", tClass.Identifier, tClass.MoneyFactor, "}"));
          }

          stream.WriteLine(string.Format("    {0}0x{1:X}, 5{2},", "{", database.MaxClassCount, "}"));
          stream.WriteLine("};");
        }, "src", "battle_main.c");
      }
    }
  }
}
