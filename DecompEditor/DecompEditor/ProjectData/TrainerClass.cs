using DecompEditor.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

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

  public class TrainerClassDatabase : ObservableObject {
    readonly Dictionary<string, TrainerClass> idToTrainerClass = new Dictionary<string, TrainerClass>();
    private ObservableCollection<TrainerClass> classes;

    // TODO: These could be configurable.
    public int MaxClassCount => 255;
    public int MaxClassNameLen => 13;
    public bool IsDirty { get; private set; }

    public ObservableCollection<TrainerClass> Classes {
      get => classes;
      private set => SetAndTrackItemUpdates(ref classes, value, this);
    }
    public TrainerClassDatabase() {
      Classes = new ObservableCollection<TrainerClass>();
      PropertyChanged += (sender, e) => IsDirty = true;
    }

    public void reset() {
      Classes.Clear();
      idToTrainerClass.Clear();
      IsDirty = false;
    }

    /// Only used during serialization.
    internal TrainerClass getClassFromId(string id) => idToTrainerClass[id];
    public void addClass(TrainerClass newClass) {
      idToTrainerClass.Add(newClass.Identifier, newClass);
      Classes.Add(newClass);
    }

    public void load(string projectDir) {
      reset();
      Deserializer.deserialize(projectDir, this);
      IsDirty = false;
    }
    public void save(string projectDir) {
      if (IsDirty) {
        Serializer.serialize(projectDir, this);
        IsDirty = false;
      }
    }

    class Deserializer {
      public static void deserialize(string projectDir, TrainerClassDatabase database) {
        loadClassNames(projectDir, database);
        loadMoneyFactors(projectDir, database);
      }

      static void loadClassNames(string projectDir, TrainerClassDatabase database) {
        StreamReader reader = File.OpenText(Path.Combine(projectDir, "src", "data", "text", "trainer_class_names.h"));
        reader.ReadLine();

        while (!reader.EndOfStream) {
          if (CParser.Element.tryDeserializeBracketString(reader.ReadLine(), out string classEnum, out string className)) {
            var tClass = new TrainerClass() {
              Identifier = classEnum.Remove(0, "TRAINER_CLASS_".Length),
              Name = className
            };
            database.addClass(tClass);
          }
        }
      }
      static void loadMoneyFactors(string projectDir, TrainerClassDatabase database) {
        StreamReader reader = File.OpenText(Path.Combine(projectDir, "src", "battle_main.c"));

        while (!reader.EndOfStream && !reader.ReadLine().EndsWith("gTrainerMoneyTable[] ="))
          continue;
        reader.ReadLine();

        while (!reader.EndOfStream) {
          string line = reader.ReadLine().Trim();
          if (!line.StartsWith("{TRAINER_CLASS"))
            break;
          int skipLen = "{TRAINER_CLASS_".Length;
          line = line.Substring(skipLen, line.Length - skipLen - "},".Length);
          string[] elements = line.Split(", ");
          if (database.idToTrainerClass.TryGetValue(elements[0], out TrainerClass tClass))
            tClass.MoneyFactor = int.Parse(elements[1]);
        }
        reader.Close();
      }
    }
    class Serializer {
      public static void serialize(string projectDir, TrainerClassDatabase database) {
        saveClassIDs(projectDir, database);
        saveClassNames(projectDir, database);
        saveMoneyFactors(projectDir, database);
      }
      static void saveClassIDs(string projectDir, TrainerClassDatabase database) {
        string[] curLines = File.ReadAllLines(Path.Combine(projectDir, "include", "constants", "trainers.h"));
        var writer = new StreamWriter(Path.Combine(projectDir, "include", "constants", "trainers.h"), false);

        // Copy the existing non trainer class ID lines.
        int curLine = 0;
        while (!curLines[curLine].StartsWith("#define TRAINER_CLASS_"))
          writer.WriteLine(curLines[curLine++]);
        while (curLines[++curLine].StartsWith("#define TRAINER_CLASS_"))
          continue;

        int longestClassID = 4;
        foreach (TrainerClass tClass in database.Classes)
          longestClassID = Math.Max(longestClassID, tClass.Identifier.Length);

        int classCount = 0;
        foreach (TrainerClass tClass in database.Classes) {
          writer.WriteLine(string.Format("#define TRAINER_CLASS_" + "{0}".PadRight(longestClassID) + " 0x{1:X}",
                                         tClass.Identifier, classCount++));
        }

        while (curLine != curLines.Length)
          writer.WriteLine(curLines[curLine++]);
        writer.Close();
      }
      static void saveClassNames(string projectDir, TrainerClassDatabase database) {
        var writer = new StreamWriter(Path.Combine(projectDir, "src", "data", "text", "trainer_class_names.h"), false);
        writer.WriteLine("const u8 gTrainerClassNames[][13] = {");
        foreach (TrainerClass tClass in database.Classes)
          writer.WriteLine(string.Format("    [TRAINER_CLASS_{0}] = _(\"{1}\"),", tClass.Identifier, tClass.Name));
        writer.WriteLine("};");
        writer.Close();
      }
      static void saveMoneyFactors(string projectDir, TrainerClassDatabase database) {
        string[] curLines = File.ReadAllLines(Path.Combine(projectDir, "src", "battle_main.c"));
        var writer = new StreamWriter(Path.Combine(projectDir, "src", "battle_main.c"), false);

        // Copy the existing lines trainer class IDs.
        int curLine = 0;
        while (!curLines[curLine].EndsWith("gTrainerMoneyTable[] ="))
          writer.WriteLine(curLines[curLine++]);
        while (!curLines[++curLine].StartsWith("}"))
          continue;

        writer.WriteLine("const struct TrainerMoney gTrainerMoneyTable[] =");
        writer.WriteLine("{");
        foreach (TrainerClass tClass in database.Classes) {
          if (tClass.MoneyFactor != 0)
            writer.WriteLine(string.Format("    {0}TRAINER_CLASS_{1}, {2}{3},", "{", tClass.Identifier, tClass.MoneyFactor, "}"));
        }

        writer.WriteLine(string.Format("    {0}0x{1:X}, 5{2},", "{", database.MaxClassCount, "}"));

        while (curLine != curLines.Length)
          writer.WriteLine(curLines[curLine++]);
        writer.Close();
      }
    }
  }
}
