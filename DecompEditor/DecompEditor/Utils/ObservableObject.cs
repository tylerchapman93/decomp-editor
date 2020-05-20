using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DecompEditor.Utils {
  public class ObservableObject : GalaSoft.MvvmLight.ObservableObject {
    Dictionary<ObservableObject, PropertyChangedEventHandler> childHandlers;

    private void addChildHandler(ObservableObject child, string propertyName) {
      if (child == null)
        return;
      if (childHandlers == null)
        childHandlers = new Dictionary<ObservableObject, PropertyChangedEventHandler>();
      PropertyChangedEventHandler handler = (sender, e) => RaisePropertyChanged(propertyName);
      childHandlers.Add(child, handler);
      child.PropertyChanged += handler;
    }
    private void removeChildHandler(ObservableObject child) {
      if (child == null || childHandlers == null)
        return;
      if (childHandlers.TryGetValue(child, out PropertyChangedEventHandler handler))
        child.PropertyChanged -= handler;
    }

    protected new bool Set<T>(ref T field, T newValue, [CallerMemberName] string propertyName = null) {
      removeChildHandler(field as ObservableObject);
      addChildHandler(newValue as ObservableObject, propertyName);
      return base.Set(propertyName, ref field, newValue);
    }
  }
}
