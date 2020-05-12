using System;
using System.Collections.Generic;

namespace CCache
{
    public class DateTimeHelper
    {
        public static Int64 UnixTickNow
        {
            get
            {
                TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
                return t.Ticks;
            }
        }

        public static DateTime Unix0
        {
            get => new DateTime(1970, 1, 1);
        }
        
    }

    public static class LinkedListHelper
    {
        public static void MoveToFront(this LinkedList<Item> linkedList, LinkedListNode<Item> node)
        {
            linkedList.Remove(node);
            linkedList.AddBefore(linkedList.First, node);
        }
    }
}