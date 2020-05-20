using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DecompEditor.Utils {
  public static class CollectionUtils {
    public static void trackItemPropertyUpdates<ValueT>(this ObservableCollection<ValueT> collection, ObservableObject parent, [CallerMemberName] string propertyName = null) {
      collection.CollectionChanged += (sender, e) => {
        parent.RaisePropertyChanged(propertyName);
      };

      var handler = new PropertyChangedEventHandler((sender, e) => {
        parent.RaisePropertyChanged(propertyName);
      });
      collection.CollectionChanged += (sender, e) => {
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
    }
  }
}
