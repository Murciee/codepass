using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Codepass
{
    public sealed class HistoryListBox : ListBox
    {
        readonly IList<UIElement> childrenView;

        public HistoryListBox()
        { childrenView = new RealizedChildren(this); }

        public IList<UIElement> Children { get { return childrenView; } }

        sealed class RealizedChildren : IList<UIElement>
        {
            readonly HistoryListBox owner;
            internal RealizedChildren(HistoryListBox owner) { this.owner = owner; }
            public UIElement this[int index]
            {
                get
                {
                    if (index < 0 || index >= owner.Items.Count) throw new ArgumentOutOfRangeException("index");
                    object item = owner.Items[index];
                    owner.ApplyTemplate(); owner.UpdateLayout();
                    ListBoxItem container = owner.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem;
                    if (container == null)
                    {
                        owner.ScrollIntoView(item); owner.UpdateLayout();
                        container = owner.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem;
                    }
                    if (container == null) return null;
                    HistoryRowControl direct = container.Content as HistoryRowControl;
                    return direct ?? Find<HistoryRowControl>(container);
                }
                set { throw new NotSupportedException(); }
            }
            public int Count { get { return owner.Items.Count; } }
            public bool IsReadOnly { get { return true; } }
            public int IndexOf(UIElement item) { return -1; }
            public void Insert(int index, UIElement item) { throw new NotSupportedException(); }
            public void RemoveAt(int index) { throw new NotSupportedException(); }
            public void Add(UIElement item) { throw new NotSupportedException(); }
            public void Clear() { throw new NotSupportedException(); }
            public bool Contains(UIElement item) { return false; }
            public void CopyTo(UIElement[] array, int index)
            { for (int i = 0; i < Count; i++) array[index + i] = this[i]; }
            public bool Remove(UIElement item) { throw new NotSupportedException(); }
            public IEnumerator<UIElement> GetEnumerator()
            { for (int i = 0; i < Count; i++) yield return this[i]; }
            IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

            static T Find<T>(DependencyObject root) where T : class
            {
                if (root == null) return null;
                T direct = root as T;
                if (direct != null) return direct;
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                {
                    T found = Find<T>(VisualTreeHelper.GetChild(root, i));
                    if (found != null) return found;
                }
                return null;
            }
        }
    }
}
