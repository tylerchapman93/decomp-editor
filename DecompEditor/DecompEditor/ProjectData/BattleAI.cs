using DecompEditor.ParserUtils;
using System.Collections.Generic;
using Truncon.Collections;

namespace DecompEditor {
  public class BattleAIDatabase : DatabaseBase {
    OrderedDictionary<string, CDefine> aiScripts = new OrderedDictionary<string, CDefine>();

    public CDefine getFromId(string name) => aiScripts[name];
    public IEnumerable<CDefine> AIScripts => aiScripts.Values;

    protected override void reset() => aiScripts.Clear();

    protected override void deserialize(ProjectDeserializer serializer) => aiScripts = serializer.parseDefineNames("AI_SCRIPT_", "include", "constants", "battle_ai.h");
  }
}
