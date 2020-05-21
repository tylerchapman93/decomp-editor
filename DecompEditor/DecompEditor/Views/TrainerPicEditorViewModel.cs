﻿using GalaSoft.MvvmLight;
using System.Collections.ObjectModel;

namespace DecompEditor.Views {
  public class TrainerPicEditorViewModel : ViewModelBase {
    TrainerPic currentPic;

    public TrainerPic CurrentPic {
      get => currentPic;
      set {
        Set(ref currentPic, value);
        RaisePropertyChanged("PicIsSelected");
      }
    }
    public bool PicIsSelected => currentPic != null;

    /// <summary>
    /// The set of sprite pics within the project.
    /// </summary>
    public ObservableCollection<TrainerPic> TrainerPics => Project.Instance.TrainerPics.FrontPics;
  }
}
