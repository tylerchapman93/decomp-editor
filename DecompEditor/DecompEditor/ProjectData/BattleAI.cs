using DecompEditor.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DecompEditor {
  public class BattleAIDatabase {
    readonly List<KeyValuePair<string, string>> enumAndName = new List<KeyValuePair<string, string>>();
    readonly Dictionary<string, int> nameOrEnumToIndex = new Dictionary<string, int>();

    public string getEnumFromName(string name) => enumAndName[nameOrEnumToIndex[name]].Key;
    public string getNameFromEnum(string name) => enumAndName[nameOrEnumToIndex[name]].Value;
    public string[] getEnumsFromNames(IEnumerable<string> names) {
      string[] enums = names.Select(name => getEnumFromName(name)).ToArray();
      int[] keys = enums.Select(flag => nameOrEnumToIndex[flag]).ToArray();
      Array.Sort(enums, keys);
      return enums;
    }
    public IEnumerable<string> AIScripts {
      get {
        string[] names = enumAndName.Select(kv => kv.Value).ToArray();
        Array.Sort(names);
        return names;
      }
    }

    public void reset() {
      enumAndName.Clear();
      nameOrEnumToIndex.Clear();
    }

    public void load(string projectDir) {
      reset();

      StreamReader reader = File.OpenText(Path.Combine(projectDir, "include", "constants", "battle_ai.h"));
      while (!reader.EndOfStream) {
        if (!reader.ReadLine().tryExtractPrefix("#define AI_SCRIPT_", " ", out string enumName))
          continue;
        string prettyName = enumName.fromSnakeToPascalSentence();
        enumName = enumName.Insert(0, "AI_SCRIPT_");
        nameOrEnumToIndex[enumName] = nameOrEnumToIndex[prettyName] = enumAndName.Count;
        enumAndName.Add(new KeyValuePair<string, string>(enumName, prettyName));
      }
    }
  }
}
