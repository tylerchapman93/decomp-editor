using DecompEditor.Utils;
using System.Collections.Generic;
using System.IO;
using Truncon.Collections;

namespace DecompEditor {
  public class Move {
    public string Identifier { get; set; }
    public string Name { get; set; }
  }
  public class MoveDatabase {
    readonly OrderedDictionary<string, Move> idToMove = new OrderedDictionary<string, Move>();

    public IEnumerable<Move> Moves => idToMove.Values;

    public Move getFromId(string id) => idToMove[id];

    public void reset() => idToMove.Clear();

    public void load(string projectDir) {
      reset();

      StreamReader reader = File.OpenText(Path.Combine(projectDir, "src", "data", "text", "move_names.h"));
      reader.ReadLine();
      reader.ReadLine();

      while (!reader.EndOfStream) {
        if (CParser.Element.tryDeserializeBracketString(reader.ReadLine(), out string moveEnum, out string moveName)) {
          idToMove.Add(moveEnum, new Move() {
            Identifier = moveEnum,
            Name = moveName
          });
        }
      }
    }
  }
}
