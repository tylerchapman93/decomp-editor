using DecompEditor.ParserUtils;
using DecompEditor.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using Truncon.Collections;

namespace DecompEditor {
  /// <summary>
  /// This class represents a specific trainer front pic.
  /// </summary>
  public class TrainerPic : ObservableObject {
    private string fullPath;
    private string path;
    private string palettePath;
    private string identifier;
    private int coordSize = 8;
    private int coordYOffset = 1;
    private int uncompressedSize;

    /// <summary>
    /// The full file path of the picture.
    /// </summary>
    public string FullPath {
      get => fullPath;
      set => Set(ref fullPath, FileUtils.normalizePath(value));
    }
    /// <summary>
    /// The project relative path of the picture.
    /// </summary>
    public string Path { get => path; set => Set(ref path, value); }
    /// <summary>
    /// The path of the palette used for the picture.
    /// </summary>
    public string PalettePath { get => palettePath; set => Set(ref palettePath, value); }
    /// <summary>
    /// The C identifier of the picture.
    /// </summary>
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
    /// <summary>
    /// The coordinate size of the picture.
    /// </summary>
    public int CoordSize { get => coordSize; set => Set(ref coordSize, value); }
    /// <summary>
    /// The Y-coordinate offset of the picture.
    /// </summary>
    public int CoordYOffset { get => coordYOffset; set => Set(ref coordYOffset, value); }
    /// <summary>
    /// The uncompressed size of the picture.
    /// </summary>
    public int UncompressedSize { get => uncompressedSize; set => Set(ref uncompressedSize, value); }
  }
  public class TrainerPicDatabase : DatabaseBase {
    readonly OrderedDictionary<string, TrainerPic> idToPic = new OrderedDictionary<string, TrainerPic>();
    private ObservableCollection<TrainerPic> frontPics;

    /// <summary>
    /// The name of the database.
    /// </summary>
    public override string Name => "Trainer Front Pic Database";

    /// <summary>
    /// Returns all of the front trainer pics within the project.
    /// </summary>
    public ObservableCollection<TrainerPic> FrontPics {
      get => frontPics;
      set => SetAndTrackItemUpdates(ref frontPics, value, this);
    }

    public TrainerPicDatabase() => FrontPics = new ObservableCollection<TrainerPic>();

    /// Only used during serialization.
    internal TrainerPic getFromID(string id) {
      if (!idToPic.TryGetValue(id, out TrainerPic pic))
        addFrontPic(pic = new TrainerPic() { Identifier = id });
      return pic;
    }
    /// <summary>
    /// Add a new trainer front pic to the project.
    /// </summary>
    /// <param name="newPic"></param>
    public void addFrontPic(TrainerPic newPic) {
      idToPic.Add(newPic.Identifier, newPic);
      FrontPics.Add(newPic);
    }

    /// <summary>
    /// Reset the data within this database.
    /// </summary>
    protected override void reset() {
      idToPic.Clear();
      FrontPics.Clear();
    }

    /// <summary>
    /// Deserialize the trainer pics from the project directory.
    /// </summary>
    /// <param name="deserializer"></param>
    protected override void deserialize(ProjectDeserializer deserializer)
      => Deserializer.deserialize(deserializer, this);

    /// <summary>
    /// Serialize the trainer pics to the project directory.
    /// </summary>
    /// <param name="serializer"></param>
    protected override void serialize(ProjectSerializer serializer)
      => Serializer.serialize(serializer, this);

    class Deserializer {
      public static void deserialize(ProjectDeserializer deserializer, TrainerPicDatabase database) {
        loadPicAndPalettePath(deserializer, database);
        loadPicTables(deserializer, database);
      }
      static void loadPicAndPalettePath(ProjectDeserializer deserializer, TrainerPicDatabase database) {
        var fileDeserializer = new FileDeserializer();
        fileDeserializer.add(new IncBinDeserializer("gTrainerFrontPic_", "u32", (cppVar, fileName) => {
          cppVar = cppVar.fromPascalToSnake();
          if (!database.idToPic.TryGetValue(cppVar, out TrainerPic trainerPic))
            database.addFrontPic(trainerPic = new TrainerPic { Identifier = cppVar });
          trainerPic.Path = fileName.Remove(0, "graphics/trainers/front_pics/".Length);
          trainerPic.FullPath = Path.Combine(deserializer.project.ProjectDir, fileName + ".png");
        }));
        fileDeserializer.add(new IncBinDeserializer("gTrainerPalette_", "u32", (cppVar, fileName) => {
          cppVar = cppVar.fromPascalToSnake();
          if (!database.idToPic.TryGetValue(cppVar, out TrainerPic trainerPic))
            database.addFrontPic(trainerPic = new TrainerPic { Identifier = cppVar });
          trainerPic.PalettePath = Path.ChangeExtension(fileName, null).Remove(0, "graphics/trainers/palettes/".Length);
        }));
        deserializer.deserializeFile(fileDeserializer, "src", "data", "graphics", "trainers.h");
      }
      static void loadPicTables(ProjectDeserializer deserializer, TrainerPicDatabase database) {
        deserializer.deserializeFile((stream) => {
          string line = stream.ReadLine();
          if (line.tryExtractPrefix("    TRAINER_SPRITE(", ",", out string classEnum)) {
            if (!database.idToPic.TryGetValue(classEnum, out TrainerPic existingPic))
              return;
            int sizeIndex = line.LastIndexOf(' ') + 1;
            existingPic.UncompressedSize = Convert.ToInt32(line.Substring(sizeIndex, line.Length - sizeIndex - 2), 16);
            return;
          }
          if (!line.tryExtractPrefix("    [TRAINER_PIC_", "]", out classEnum) ||
              !database.idToPic.TryGetValue(classEnum, out TrainerPic trainerPic)) {
            return;
          }

          int structStartIndex = line.LastIndexOf('{') + ".size = ".Length + 1;
          int structSplitIndex = line.IndexOf(',', structStartIndex);
          trainerPic.CoordSize = int.Parse(line.Substring(structStartIndex, structSplitIndex - structStartIndex));
          structSplitIndex += ", .y_offset = ".Length;
          trainerPic.CoordYOffset = int.Parse(line.Substring(structSplitIndex, line.Length - structSplitIndex - 2));
        }, "src", "data", "trainer_graphics", "front_pic_tables.h");
      }
    }

    class Serializer {
      public static void serialize(ProjectSerializer serializer, TrainerPicDatabase database) {
        savePicAndPalettePath(serializer, database);
        savePicAndPalettePathExterns(serializer, database);
        savePicTables(serializer, database);
        saveAnimTables(serializer, database);
        savePicIDs(serializer, database);
      }
      static void savePicAndPalettePath(ProjectSerializer serializer, TrainerPicDatabase database) {
        string projectDir = serializer.project.ProjectDir;
        string[] curLines = File.ReadAllLines(Path.Combine(projectDir, "src", "data", "graphics", "trainers.h"));
        serializer.serializeFile(stream => {
          // Copy all of the current lines not related to front pics.
          // TODO: This wouldn't be necessary if we also handled back sprites.
          foreach (string line in curLines) {
            if (line.Length != 0 && !line.StartsWith("const u32 gTrainerFrontPic_") &&
                !line.StartsWith("const u32 gTrainerPalette_")) {
              stream.WriteLine(line);
            }
          }
          stream.WriteLine();

          string picFormat = "const u32 gTrainerFrontPic_{0}[] = INCBIN_U32(\"graphics/trainers/front_pics/{1}.4bpp.lz\");";
          string palFormat = "const u32 gTrainerPalette_{0}[] = INCBIN_U32(\"graphics/trainers/palettes/{1}.gbapal.lz\");";
          foreach (TrainerPic pic in database.FrontPics) {
            string cppVar = pic.Identifier.fromSnakeToPascal();
            stream.WriteLine(string.Format(picFormat, cppVar, pic.Path));
            stream.WriteLine(string.Format(palFormat, cppVar, pic.PalettePath));
            stream.WriteLine();
          }
        }, "src", "data", "graphics", "trainers.h");

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
      static void savePicAndPalettePathExterns(ProjectSerializer serializer, TrainerPicDatabase database) {
        Func<string, bool> trainerPicCheck = str => str.StartsWith("extern const u32 gTrainerFrontPic_");
        Action<StreamWriter> trainerPicAction = stream => {
          foreach (TrainerPic pic in database.FrontPics)
            stream.WriteLine("extern const u32 gTrainerFrontPic_" + pic.Identifier.fromSnakeToPascal() + "[];");
        };
        Func<string, bool> trainerPalCheck = str => str.StartsWith("extern const u32 gTrainerPalette_");
        Action<StreamWriter> trainerPalAction = stream => {
          foreach (TrainerPic pic in database.FrontPics)
            stream.WriteLine("extern const u32 gTrainerPalette_" + pic.Identifier.fromSnakeToPascal() + "[];");
        };
        serializer.serializePartialFile(new[] { trainerPicCheck, trainerPalCheck },
                                        new[] { trainerPicAction, trainerPalAction },
                                        "include", "graphics.h");
      }
      static void savePicTables(ProjectSerializer serializer, TrainerPicDatabase database) {
        serializer.serializeFile(stream => {
          stream.WriteLine("const struct MonCoords gTrainerFrontPicCoords[] =");
          stream.WriteLine("{");
          foreach (TrainerPic pic in database.FrontPics) {
            stream.WriteLine("    [TRAINER_PIC_" + pic.Identifier + "] = {.size = " +
                             pic.CoordSize + ", .y_offset = " + pic.CoordYOffset + "},");
          }
          stream.WriteLine("};\n");

          stream.WriteLine("const struct CompressedSpriteSheet gTrainerFrontPicTable[] =");
          stream.WriteLine("{");
          foreach (TrainerPic pic in database.FrontPics) {
            stream.WriteLine("    TRAINER_SPRITE(" + pic.Identifier +
                             ", gTrainerFrontPic_" + pic.Identifier.fromSnakeToPascal() +
                             ", 0x" + string.Format("{0:X}", pic.UncompressedSize) + "),");
          }
          stream.WriteLine("};\n");

          stream.WriteLine("const struct CompressedSpritePalette gTrainerFrontPicPaletteTable[] =");
          stream.WriteLine("{");
          foreach (TrainerPic pic in database.FrontPics) {
            stream.WriteLine("    TRAINER_PAL(" + pic.Identifier + ", gTrainerPalette_" +
                             pic.Identifier.fromSnakeToPascal() + "),");
          }
          stream.WriteLine("};\n");
        }, "src", "data", "trainer_graphics", "front_pic_tables.h");
      }
      static void saveAnimTables(ProjectSerializer serializer, TrainerPicDatabase database) {
        serializer.serializeFile(stream => {
          foreach (TrainerPic pic in database.FrontPics) {
            stream.WriteLine("static const union AnimCmd *const sAnims_" +
                             pic.Identifier.fromSnakeToPascal() + "[] ={");
            stream.WriteLine("    sAnim_GeneralFrame0,");
            stream.WriteLine("};\n");
          }

          stream.WriteLine("const union AnimCmd *const *const gTrainerFrontAnimsPtrTable[] =");
          stream.WriteLine("{");
          foreach (TrainerPic pic in database.FrontPics) {
            stream.WriteLine("    [TRAINER_PIC_" + pic.Identifier + "] = sAnims_" +
                             pic.Identifier.fromSnakeToPascal() + ",");
          }
          stream.WriteLine("};\n");
        }, "src", "data", "trainer_graphics", "front_pic_anims.h");
      }
      static void savePicIDs(ProjectSerializer serializer, TrainerPicDatabase database) {
        serializer.serializePartialFile(str => str.StartsWith("#define TRAINER_PIC_"), stream => {
          int longestPicID = 0;
          foreach (TrainerPic pic in database.FrontPics)
            longestPicID = Math.Max(longestPicID, pic.Identifier.Length);

          int picCount = 0;
          foreach (TrainerPic pic in database.FrontPics)
            stream.WriteLine("#define TRAINER_PIC_" + pic.Identifier.PadRight(longestPicID + 1) + picCount++);
        }, "include", "constants", "trainers.h");
      }
    }
  }
}
