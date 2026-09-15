using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace KTWirzade.GUI
{
    public static class IDragMethods
    {
        public static int FindPlaybookIndex(this ObservableCollection<IDragItem> items, Predicate<PlaybookGUI> match)
        {
            return items.ToList().FindIndex((IDragItem item) => item is PlaybookGUI obj && match(obj));
        }

        public static int FindISOIndex(this ObservableCollection<IDragItem> items, Predicate<ISO> match)
        {
            return items.ToList().FindIndex((IDragItem item) => item is ISO obj && match(obj));
        }
    }
}