using DecompEditor.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using Truncon.Collections;

namespace DecompEditor {
  public class TrainerPic {
    public string FileName { get; set; }
    public string PaletteFileName { get; set; }
    public string Identifier { get; set; }
    public int CoordSize { get; set; }
    public int CoordYOffset { get; set; }
    public int UncompressedSize { get; set; }
  }
  public class TrainerPicDatabase {
    readonly OrderedDictionary<string, TrainerPic> idToPic = new OrderedDictionary<string, TrainerPic>();

    public IEnumerable<TrainerPic> TrainerPics => idToPic.Values;
    public TrainerPic getFromID(string id) {
      if (!idToPic.TryGetValue(id, out TrainerPic pic)) {
        idToPic.Add(id, pic = new TrainerPic() {
          Identifier = id
        });
      }
      return pic;
    }

    public void reset() => idToPic.Clear();

    public void load(string projectDir) {
      reset();

      loadPicAndPalettePath(projectDir);
      loadSpriteData(projectDir);
    }
    void loadPicAndPalettePath(string projectDir) {
      StreamReader reader = File.OpenText(Path.Combine(projectDir, "src", "data", "graphics", "trainers.h"));

      string classEnum;
      TrainerPic trainerPic;
      while (!reader.EndOfStream) {
        string line = reader.ReadLine();
        if (line.StartsWith("const u32 gTrainerFrontPic_")) {
          int skipLen = "const u32 gTrainerFrontPic_".Length;
          classEnum = line.Substring(skipLen, line.IndexOf('[') - skipLen).fromPascalToSnake();
          if (!idToPic.TryGetValue(classEnum, out trainerPic)) {
            trainerPic = new TrainerPic {
              Identifier = classEnum
            };
            idToPic.Add(classEnum, trainerPic);
          }

          skipLen = line.IndexOf('\"') + 1;
          trainerPic.FileName = line.Substring(skipLen, line.IndexOf('.', skipLen) - skipLen) + ".png";
          continue;
        }

        if (line.StartsWith("const u32 gTrainerPalette_")) {
          int skipLen = "const u32 gTrainerPalette_".Length;
          classEnum = line.Substring(skipLen, line.IndexOf('[') - skipLen).fromPascalToSnake();
          if (!idToPic.TryGetValue(classEnum, out trainerPic)) {
            trainerPic = new TrainerPic {
              Identifier = classEnum
            };
            idToPic.Add(classEnum, trainerPic);
          }

          skipLen = line.IndexOf('\"') + 1;
          trainerPic.PaletteFileName = line.Substring(skipLen, line.IndexOf('.', skipLen) - skipLen) + ".pal";
          continue;
        }
      }
    }
    void loadSpriteData(string projectDir) {
      StreamReader reader = File.OpenText(Path.Combine(projectDir, "src", "data", "trainer_graphics", "front_pic_tables.h"));
      reader.ReadLine();

      string classEnum;
      TrainerPic trainerPic;
      while (!reader.EndOfStream) {
        string line = reader.ReadLine();
        if (line.StartsWith("    TRAINER_SPRITE(")) {
          int skipLen = "    TRAINER_SPRITE(".Length;
          classEnum = line.Substring(skipLen, line.IndexOf(',', skipLen) - skipLen);
          if (!idToPic.TryGetValue(classEnum, out trainerPic))
            continue;
          int sizeIndex = line.LastIndexOf(' ') + 1;
          string str = line.Substring(sizeIndex, line.Length - sizeIndex - 2);
          trainerPic.UncompressedSize = Convert.ToInt32(line.Substring(sizeIndex, line.Length - sizeIndex - 2), 16);
          continue;
        }

        if (!line.StartsWith("    ["))
          continue;
        classEnum = line.Substring(5, line.IndexOf(']') - 5);
        if (!idToPic.TryGetValue(classEnum, out trainerPic))
          continue;

        int structStartIndex = line.LastIndexOf('{') + ".size = ".Length + 1;
        int structSplitIndex = line.IndexOf(',', structStartIndex);
        trainerPic.CoordSize = int.Parse(line.Substring(structStartIndex, structSplitIndex - structStartIndex));
        structSplitIndex += ", .y_offset = ".Length;
        trainerPic.CoordYOffset = int.Parse(line.Substring(structSplitIndex, line.Length - structSplitIndex - 2));
      }
    }
  }
}
