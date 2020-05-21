using DecompEditor.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using Truncon.Collections;

namespace DecompEditor {
  public class TrainerPic : ObservableObject {
    private string fullPath;
    private string path;
    private string palettePath;
    private string identifier;
    private int coordSize = 8;
    private int coordYOffset = 1;
    private int uncompressedSize;

    public string FullPath {
      get => fullPath;
      set => Set(ref fullPath, FileUtils.normalizePath(value));
    }
    public string Path { get => path; set => Set(ref path, value); }
    public string PalettePath { get => palettePath; set => Set(ref palettePath, value); }
    public string Identifier {
      get => identifier;
      set {
        if (identifier != null) {
          string curCppVar = identifier.fromSnakeToPascal();
          string newCppVar = value.fromSnakeToPascal();
          Project.Instance.registerFileReplacement("TRAINER_PIC_" + identifier,
                                                   "TRAINER_PIC_" + value);
          Project.Instance.registerFileReplacement("gTrainerPalette_" + curCppVar,
                                                   "gTrainerPalette_" + newCppVar);
        }
        Set(ref identifier, value);
      }
    }
    public int CoordSize { get => coordSize; set => Set(ref coordSize, value); }
    public int CoordYOffset { get => coordYOffset; set => Set(ref coordYOffset, value); }
    public int UncompressedSize { get => uncompressedSize; set => Set(ref uncompressedSize, value); }
  }
  public class TrainerPicDatabase : ObservableObject {
    readonly OrderedDictionary<string, TrainerPic> idToPic = new OrderedDictionary<string, TrainerPic>();
    private ObservableCollection<TrainerPic> frontPics;

    public ObservableCollection<TrainerPic> FrontPics {
      get => frontPics;
      set => SetAndTrackItemUpdates(ref frontPics, value, this);
    }
    public bool IsDirty {
      get;
      private set;
    }

    public TrainerPicDatabase() {
      FrontPics = new ObservableCollection<TrainerPic>();
      PropertyChanged += (sender, e) => IsDirty = true;
    }

    /// Only used during serialization.
    internal TrainerPic getFromID(string id) {
      if (!idToPic.TryGetValue(id, out TrainerPic pic)) {
        idToPic.Add(id, pic = new TrainerPic() {
          Identifier = id
        });
        FrontPics.Add(pic);
      }
      return pic;
    }
    public void addFrontPic(TrainerPic newPic) {
      idToPic.Add(newPic.Identifier, newPic);
      FrontPics.Add(newPic);
    }

    public void reset() {
      idToPic.Clear();
      FrontPics.Clear();
      IsDirty = false;
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
      public static void deserialize(string projectDir, TrainerPicDatabase database) {
        loadPicAndPalettePath(projectDir, database);
        loadPicTables(projectDir, database);
      }
      static void loadPicAndPalettePath(string projectDir, TrainerPicDatabase database) {
        StreamReader reader = File.OpenText(Path.Combine(projectDir, "src", "data", "graphics", "trainers.h"));

        string classEnum;
        TrainerPic trainerPic;
        while (!reader.EndOfStream) {
          string line = reader.ReadLine();
          if (line.StartsWith("const u32 gTrainerFrontPic_")) {
            int skipLen = "const u32 gTrainerFrontPic_".Length;
            classEnum = line.Substring(skipLen, line.IndexOf('[') - skipLen).fromPascalToSnake();
            if (!database.idToPic.TryGetValue(classEnum, out trainerPic)) {
              trainerPic = new TrainerPic {
                Identifier = classEnum
              };
              database.idToPic.Add(classEnum, trainerPic);
              database.FrontPics.Add(trainerPic);
            }

            skipLen = line.IndexOf('\"') + 1;
            string path = line.Substring(skipLen, line.IndexOf('.', skipLen) - skipLen);
            trainerPic.Path = path.Remove(0, "graphics/trainers/front_pics/".Length);
            trainerPic.FullPath = Path.Combine(projectDir, path + ".png");
            continue;
          }

          if (line.StartsWith("const u32 gTrainerPalette_")) {
            int skipLen = "const u32 gTrainerPalette_".Length;
            classEnum = line.Substring(skipLen, line.IndexOf('[') - skipLen).fromPascalToSnake();
            if (!database.idToPic.TryGetValue(classEnum, out trainerPic)) {
              trainerPic = new TrainerPic {
                Identifier = classEnum
              };
              database.idToPic.Add(classEnum, trainerPic);
              database.FrontPics.Add(trainerPic);
            }

            skipLen = line.IndexOf('\"') + 1;
            trainerPic.PalettePath = line.Substring(skipLen, line.IndexOf('.', skipLen) - skipLen)
                                         .Remove(0, "graphics/trainers/palettes/".Length);
            continue;
          }
        }
      }
      static void loadPicTables(string projectDir, TrainerPicDatabase database) {
        StreamReader reader = File.OpenText(Path.Combine(projectDir, "src", "data", "trainer_graphics", "front_pic_tables.h"));
        reader.ReadLine();

        TrainerPic trainerPic;
        while (!reader.EndOfStream) {
          string line = reader.ReadLine();
          if (line.tryExtractPrefix("    TRAINER_SPRITE(", ",", out string classEnum)) {
            if (!database.idToPic.TryGetValue(classEnum, out trainerPic))
              continue;
            int sizeIndex = line.LastIndexOf(' ') + 1;
            trainerPic.UncompressedSize = Convert.ToInt32(line.Substring(sizeIndex, line.Length - sizeIndex - 2), 16);
            continue;
          }

          if (!line.tryExtractPrefix("    [TRAINER_PIC_", "]", out classEnum))
            continue;
          if (!database.idToPic.TryGetValue(classEnum, out trainerPic))
            continue;

          int structStartIndex = line.LastIndexOf('{') + ".size = ".Length + 1;
          int structSplitIndex = line.IndexOf(',', structStartIndex);
          trainerPic.CoordSize = int.Parse(line.Substring(structStartIndex, structSplitIndex - structStartIndex));
          structSplitIndex += ", .y_offset = ".Length;
          trainerPic.CoordYOffset = int.Parse(line.Substring(structSplitIndex, line.Length - structSplitIndex - 2));
        }
      }
    }

    class Serializer {
      public static void serialize(string projectDir, TrainerPicDatabase database) {
        savePicAndPalettePath(projectDir, database);
        savePicAndPalettePathExterns(projectDir, database);
        savePicTables(projectDir, database);
        saveAnimTables(projectDir, database);
        savePicIDs(projectDir, database);
      }
      static void savePicAndPalettePath(string projectDir, TrainerPicDatabase database) {
        string[] curLines = File.ReadAllLines(Path.Combine(projectDir, "src", "data", "graphics", "trainers.h"));
        var writer = new StreamWriter(Path.Combine(projectDir, "src", "data", "graphics", "trainers.h"), false);

        // Copy all of the current lines not related to front pics.
        // TODO: This wouldn't be necessary if we also handled back sprites.
        foreach (string line in curLines) {
          if (line.Length != 0 && !line.StartsWith("const u32 gTrainerFrontPic_") &&
              !line.StartsWith("const u32 gTrainerPalette_")) {
            writer.WriteLine(line);
          }
        }
        writer.WriteLine();

        string picFormat = "const u32 gTrainerFrontPic_{0}[] = INCBIN_U32(\"graphics/trainers/front_pics/{1}.4bpp.lz\");";
        string palFormat = "const u32 gTrainerPalette_{0}[] = INCBIN_U32(\"graphics/trainers/palettes/{1}.gbapal.lz\");";
        foreach (TrainerPic pic in database.FrontPics) {
          string cppVar = pic.Identifier.fromSnakeToPascal();
          writer.WriteLine(string.Format(picFormat, cppVar, pic.Path));
          writer.WriteLine(string.Format(palFormat, cppVar, pic.PalettePath));
          writer.WriteLine();
        }

        writer.Close();

        // Check to see if any of the pics changed location.
        foreach (TrainerPic pic in database.FrontPics) {
          string fullPrettyPath = Path.Combine(projectDir, "graphics/trainers/front_pics", pic.Path + ".png");
          string normalizedPath = FileUtils.normalizePath(fullPrettyPath);
          if (pic.FullPath == normalizedPath)
            continue;
          File.Copy(pic.FullPath, fullPrettyPath, true);
          pic.FullPath = normalizedPath;

          // Generate a new palette file.
          IList<Color> palette = FileUtils.loadBitmapImage(normalizedPath).Palette.Colors;
          string normalizePalPath = Path.Combine(projectDir, "graphics/trainers/palettes", pic.PalettePath + ".pal");
          using (var palWriter = new StreamWriter(normalizePalPath, false)) {
            palWriter.WriteLine("JASC-PAL");
            palWriter.WriteLine("0100");
            palWriter.WriteLine(palette.Count);
            foreach (Color color in palette)
              palWriter.WriteLine("{0} {1} {2}", color.R, color.G, color.B);
          }

          // Delete any existing bpp/pal files to force a rebuild.
          File.Delete(Path.ChangeExtension(normalizedPath, ".4bpp"));
          File.Delete(Path.ChangeExtension(normalizedPath, ".4bpp.lz"));
          File.Delete(Path.ChangeExtension(normalizePalPath, ".gbapal"));
          File.Delete(Path.ChangeExtension(normalizePalPath, ".gbapal.lz"));
        }
      }
      static void savePicAndPalettePathExterns(string projectDir, TrainerPicDatabase database) {
        string[] curLines = File.ReadAllLines(Path.Combine(projectDir, "include", "graphics.h"));
        var writer = new StreamWriter(Path.Combine(projectDir, "include", "graphics.h"), false);

        // Copy the existing non trainer pic ID lines.
        int curLine = 0;
        while (!curLines[curLine].StartsWith("extern const u32 gTrainerFrontPic_"))
          writer.WriteLine(curLines[curLine++]);
        while (curLines[++curLine].StartsWith("extern const u32 gTrainerFrontPic_"))
          continue;
        foreach (TrainerPic pic in database.FrontPics)
          writer.WriteLine("extern const u32 gTrainerFrontPic_" + pic.Identifier.fromSnakeToPascal() + "[];");

        while (!curLines[curLine].StartsWith("extern const u32 gTrainerPalette_"))
          writer.WriteLine(curLines[curLine++]);
        while (curLines[++curLine].StartsWith("extern const u32 gTrainerPalette_"))
          continue;

        foreach (TrainerPic pic in database.FrontPics)
          writer.WriteLine("extern const u32 gTrainerPalette_" + pic.Identifier.fromSnakeToPascal() + "[];");

        while (curLine != curLines.Length)
          writer.WriteLine(curLines[curLine++]);
        writer.Close();
      }
      static void savePicTables(string projectDir, TrainerPicDatabase database) {
        var writer = new StreamWriter(Path.Combine(projectDir, "src", "data", "trainer_graphics", "front_pic_tables.h"), false);
        writer.WriteLine("const struct MonCoords gTrainerFrontPicCoords[] =");
        writer.WriteLine("{");
        foreach (TrainerPic pic in database.FrontPics) {
          writer.WriteLine("    [TRAINER_PIC_" + pic.Identifier + "] = {.size = " +
                           pic.CoordSize + ", .y_offset = " + pic.CoordYOffset + "},");
        }
        writer.WriteLine("};\n");

        writer.WriteLine("const struct CompressedSpriteSheet gTrainerFrontPicTable[] =");
        writer.WriteLine("{");
        foreach (TrainerPic pic in database.FrontPics) {
          writer.WriteLine("    TRAINER_SPRITE(" + pic.Identifier +
                           ", gTrainerFrontPic_" + pic.Identifier.fromSnakeToPascal() +
                           ", 0x" + string.Format("{0:X}", pic.UncompressedSize) + "),");
        }
        writer.WriteLine("};\n");

        writer.WriteLine("const struct CompressedSpritePalette gTrainerFrontPicPaletteTable[] =");
        writer.WriteLine("{");
        foreach (TrainerPic pic in database.FrontPics) {
          writer.WriteLine("    TRAINER_PAL(" + pic.Identifier + ", gTrainerPalette_" +
                           pic.Identifier.fromSnakeToPascal() + "),");
        }
        writer.WriteLine("};\n");
        writer.Close();
      }
      static void saveAnimTables(string projectDir, TrainerPicDatabase database) {
        var writer = new StreamWriter(Path.Combine(projectDir, "src", "data", "trainer_graphics", "front_pic_anims.h"), false);

        foreach (TrainerPic pic in database.FrontPics) {
          writer.WriteLine("static const union AnimCmd *const sAnims_" +
                           pic.Identifier.fromSnakeToPascal() + "[] ={");
          writer.WriteLine("    sAnim_GeneralFrame0,");
          writer.WriteLine("};\n");
        }

        writer.WriteLine("const union AnimCmd *const *const gTrainerFrontAnimsPtrTable[] =");
        writer.WriteLine("{");
        foreach (TrainerPic pic in database.FrontPics) {
          writer.WriteLine("    [TRAINER_PIC_" + pic.Identifier + "] = sAnims_" +
                           pic.Identifier.fromSnakeToPascal() + ",");
        }
        writer.WriteLine("};\n");
        writer.Close();
      }
      static void savePicIDs(string projectDir, TrainerPicDatabase database) {
        string[] curLines = File.ReadAllLines(Path.Combine(projectDir, "include", "constants", "trainers.h"));
        var writer = new StreamWriter(Path.Combine(projectDir, "include", "constants", "trainers.h"), false);

        // Copy the existing non trainer pic ID lines.
        int curLine = 0;
        while (!curLines[curLine].StartsWith("#define TRAINER_PIC_"))
          writer.WriteLine(curLines[curLine++]);
        while (curLines[++curLine].StartsWith("#define TRAINER_PIC_"))
          continue;

        int longestPicID = 0;
        foreach (TrainerPic pic in database.FrontPics)
          longestPicID = Math.Max(longestPicID, pic.Identifier.Length);

        int picCount = 0;
        foreach (TrainerPic pic in database.FrontPics)
          writer.WriteLine("#define TRAINER_PIC_" + pic.Identifier.PadRight(longestPicID + 1) + picCount++);

        while (curLine != curLines.Length)
          writer.WriteLine(curLines[curLine++]);
        writer.Close();
      }
    }
  }
}
