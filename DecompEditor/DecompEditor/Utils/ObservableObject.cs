using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DecompEditor.Utils {
  public class ObservableObject : GalaSoft.MvvmLight.ObservableObject {
    Dictionary<INotifyPropertyChanged, PropertyChangedEventHandler> childHandlers;

    private void addChildHandler(INotifyPropertyChanged child, string propertyName) {
      if (child == null)
        return;
      if (childHandlers == null)
        childHandlers = new Dictionary<INotifyPropertyChanged, PropertyChangedEventHandler>();
      PropertyChangedEventHandler handler = (sender, e) => RaisePropertyChanged(propertyName);
      childHandlers.Add(child, handler);
      child.PropertyChanged += handler;
    }
    private void removeChildHandler(INotifyPropertyChanged child) {
      if (child == null || childHandlers == null)
        return;
      if (childHandlers.TryGetValue(child, out PropertyChangedEventHandler handler))
        child.PropertyChanged -= handler;
    }

    protected bool SetAndTrack<T>(ref T field, T newValue, [CallerMemberName] string propertyName = null) {
      removeChildHandler(field as INotifyPropertyChanged);
      addChildHandler(newValue as INotifyPropertyChanged, propertyName);
      return Set(propertyName, ref field, newValue);
    }
    protected bool SetAndTrackItemUpdates<ValueT>(ref ObservableCollection<ValueT> field,
                                                  ObservableCollection<ValueT> newValue,
                                                  ObservableObject parent,
                                                  [CallerMemberName] string propertyName = null) {
      var handler = new PropertyChangedEventHandler((sender, e) => {
        parent.RaisePropertyChanged(propertyName);
      });
      newValue.CollectionChanged += (sender, e) => {
        parent.RaisePropertyChanged(propertyName);

        if (e.OldItems != null) {
          foreach (object item in e.OldItems) {
            if (item is INotifyPropertyChanged)
              ((INotifyPropertyChanged)item).PropertyChanged -= handler;
          }
        }
        if (e.NewItems != null) {
          foreach (object item in e.NewItems) {
            if (item is INotifyPropertyChanged)
              ((INotifyPropertyChanged)item).PropertyChanged += handler;
          }
        }
      };

      return Set(ref field, newValue, propertyName);
    }
  }
}
