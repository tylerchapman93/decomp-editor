using DecompEditor.Views;
using Microsoft.Win32;
using System.ComponentModel;
using System.Windows;

namespace DecompEditor.Editors {
  /// <summary>
  /// Interaction logic for OverworldPicEditorWindow.xaml
  /// </summary>
  public partial class OverworldPicEditorWindow : Window {
    public OverworldPicEditorViewModel ViewModel => DataContext as OverworldPicEditorViewModel;
    public OverworldPicEditorWindow(EventObjectPic initialPic) {
      InitializeComponent();
      ViewModel.CurrentPic = initialPic;

      var identifierSortDesc = new SortDescription("Identifier", ListSortDirection.Ascending);
      spritePicList.Items.SortDescriptions.Add(identifierSortDesc);
      spritePicList.Items.IsLiveSorting = true;
    }

    private bool uploadPicture(out string path) {
      var openFileDialog = new OpenFileDialog {
        InitialDirectory = System.IO.Path.Combine(
        Project.Instance.ProjectDir, "graphics", "object_events", "pics"),
        Filter = "Sprite Image (*.png)|*.png",
        RestoreDirectory = true
      };
      bool result = openFileDialog.ShowDialog() == true;
      path = result ? openFileDialog.FileName : string.Empty;
      return result;
    }

    private void uploadNewPicButton_Click(object sender, RoutedEventArgs e) {
      if (uploadPicture(out string newPath))
        ViewModel.CurrentPic.FullPath = newPath;
    }

    private void addPicButton_Click(object sender, RoutedEventArgs e) {
      if (!uploadPicture(out string path))
        return;
      System.Collections.ObjectModel.ObservableCollection<EventObjectPic> pics = Project.Instance.EventObjects.Pics;
      string identifier = "__new_pic_" + pics.Count;
      var newPic = new EventObjectPic() {
        Identifier = identifier,
        Path = identifier,
        FullPath = path,
      };
      pics.Add(newPic);
      ViewModel.CurrentPic = newPic;
    }
  }
}
