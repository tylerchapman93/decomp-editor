using System.Windows;
using System.Windows.Media;

namespace DecompEditor.Utils {
  static class DialogUtils {
    /// Try to find the visual parent of the specified type.
    public static T FindVisualParent<T>(this DependencyObject childElement) where T : DependencyObject {
      DependencyObject parent = VisualTreeHelper.GetParent(childElement);
      if (parent == null)
        return null;
      else if (parent is T parentAsT)
        return parentAsT;
      return FindVisualParent<T>(parent);
    }
  }
}
