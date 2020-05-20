using System;
using System.Collections.Generic;
using System.IO;

namespace DecompEditor.Utils {
  class CParser {
    public enum NameKind {
      EnumBracket,
      StructElement,
    }
    public enum ValueKind {
      EnumList, // Identifier (, Identifer)*
      EnumMask, // Identifier (| Identifer)*
      Enum,     // Identifier
      Integer,  // 0-9+
      String,   // ""
    }

    public delegate void ElementValueHandler(object valueObj);
    public delegate void EnumListHandler(string[] values);
    public delegate void EnumMaskHandler(string[] values);
    public delegate void EnumHandler(string value);
    public delegate void IntegerHandler(int value);
    public delegate void StringHandler(string value);

    public class Element {
      public static bool tryDeserializeBracketString(string line, out string name, out string value) {
        value = string.Empty;
        if (!tryDeserializeName(ref line, NameKind.EnumBracket, out name))
          return false;
        string result = string.Empty;
        deserializeValue(line, ValueKind.String, (valueObj) => result = valueObj as string);
        value = result;
        return true;
      }

      public static bool tryDeserializeName(ref string line, NameKind nameKind, out string name) {
        name = string.Empty;
        switch (nameKind) {
          case NameKind.EnumBracket: {
            if (!line.StartsWith("    ["))
              return false;
            name = line.Substring(5, line.IndexOf(']') - 5);
            break;
          }
          case NameKind.StructElement: {
            line = line.TrimStart();
            if (!line.StartsWith("."))
              return false;
            name = line.Substring(1, line.IndexOf(' ') - 1);
            break;
          }
        }
        line = line.Substring(line.IndexOf('=', name.Length + 2) + 2);
        if (line[line.Length - 1] == ',')
          line = line.Remove(line.Length - 1);
        return true;
      }

      public static void deserializeValue(string line, ValueKind valueKind, ElementValueHandler handler) {
        switch (valueKind) {
          case ValueKind.EnumList: {
            if (line[0] == '{') {
              line = line.Remove(0, 1);
              line = line.Remove(line.Length - 1);
            }

            handler(line.Split(", ", StringSplitOptions.RemoveEmptyEntries));
            break;
          }
          case ValueKind.EnumMask: {
            handler(line.Split(" | "));
            break;
          }
          case ValueKind.Enum: {
            handler(line);
            break;
          }
          case ValueKind.Integer: {
            handler(int.Parse(line));
            break;
          }
          case ValueKind.String: {
            int valueIndexStart = line.IndexOf('\"') + 1;
            int valueIndexEnd = line.LastIndexOf('\"');
            handler(line.Substring(valueIndexStart, valueIndexEnd - valueIndexStart));
            break;
          }
        }
      }
    }

    public class Struct {
      class StructValue {
        public ValueKind kind;
        public ElementValueHandler deserializeHandler;
      };

      readonly List<StructValue> values = new List<StructValue>();
      readonly Dictionary<string, int> nameToElementIndex = new Dictionary<string, int>();

      public void deserialize(StreamReader stream) {
        do {
          // Try to parse an element line.
          string line = stream.ReadLine().TrimStart();
          if (!Element.tryDeserializeName(ref line, NameKind.StructElement, out string name))
            break;
          if (!nameToElementIndex.TryGetValue(name, out int elementIndex))
            continue;
          StructValue element = values[elementIndex];
          Element.deserializeValue(line, element.kind, element.deserializeHandler);
        } while (true);
      }

      public void addEnumList(string name, EnumListHandler handler) => addValue(name, ValueKind.EnumList, (obj) => handler(obj as string[]));
      public void addEnumMask(string name, EnumMaskHandler handler) => addValue(name, ValueKind.EnumMask, (obj) => handler(obj as string[]));
      public void addEnum(string name, EnumHandler handler) => addValue(name, ValueKind.Enum, (obj) => handler(obj as string));
      public void addInteger(string name, IntegerHandler handler) => addValue(name, ValueKind.Integer, (obj) => handler((int)obj));
      public void addString(string name, StringHandler handler) => addValue(name, ValueKind.String, (obj) => handler(obj as string));
      public void addValue(string name, ValueKind kind, ElementValueHandler handler) {
        nameToElementIndex.Add(name, values.Count);
        values.Add(new StructValue() {
          kind = kind,
          deserializeHandler = handler
        });
      }
    }
  }
}
