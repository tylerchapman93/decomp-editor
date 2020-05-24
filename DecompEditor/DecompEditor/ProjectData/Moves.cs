using DecompEditor.ParserUtils;
using System.Collections.Generic;
using Truncon.Collections;

namespace DecompEditor {
  public class Move {
    public string Identifier { get; set; }
    public string Name { get; set; }
  }
  public class MoveDatabase : DatabaseBase {
    readonly OrderedDictionary<string, Move> idToMove = new OrderedDictionary<string, Move>();

    public IEnumerable<Move> Moves => idToMove.Values;
    public Move getFromId(string id) => idToMove[id];

    protected override void reset() => idToMove.Clear();

    protected override void deserialize(ProjectDeserializer deserializer) {
      deserializer.deserializeFile((reader) => {
        if (!StructBodyDeserializer.Element.tryDeserializeBracketString(reader.ReadLine(), out string moveEnum, out string moveName))
          return;
        idToMove.Add(moveEnum, new Move() {
          Identifier = moveEnum,
          Name = moveName
        });
      }, "src", "data", "text", "move_names.h");
    }
  }
}
