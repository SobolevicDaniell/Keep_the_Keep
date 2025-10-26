using System.Reflection;

namespace Game
{
    public static class InventorySlotStateAccessor
    {
        private static readonly System.Type T = typeof(InventorySlotState);

        private static readonly MemberAccessor _id = FindMember(new[]
        {
            "Id","id","ItemId","itemId","ItemID","itemID","item","key"
        });

        private static readonly MemberAccessor _count = FindMember(new[]
        {
            "Count","count","Amount","amount","Qty","qty","quantity","Quantity","stack","Stack","stackCount","StackCount","value","Value","num","Num"
        });

        private static readonly MemberAccessor _state = FindMember(new[]
        {
            "State","state","itemState","ItemState","Meta","meta","Data","data","payload","Payload"
        });


        public static bool HasId  => !_id.IsMissing;
        public static bool HasCnt => !_count.IsMissing;

        public static string ReadId(InventorySlotState s)
        {
            if (s == null || _id.IsMissing) return null; return _id.Get(s) as string;
        }

        public static int ReadCount(InventorySlotState s)
        {
            if (s == null || _count.IsMissing) return 0;
            var v = _count.Get(s);
            return v is int iv ? iv : 0;
        }

        public static ItemState ReadState(InventorySlotState s)
        {
            if (s == null || _state.IsMissing) return null;
            return _state.Get(s) as ItemState;
        }

        public static void WriteId(InventorySlotState s, string v)
        {
            if (s == null || _id.IsMissing) return; _id.Set(s, v);
        }

        public static void WriteCount(InventorySlotState s, int v)
        {
            if (s == null || _count.IsMissing) return; _count.Set(s, v);
        }

        public static void WriteState(InventorySlotState s, ItemState v)
        {
            if (s == null || _state.IsMissing) return; _state.Set(s, v);
        }

        private static MemberAccessor FindMember(string[] names)
        {
            foreach (var n in names)
            {
                var p = T.GetProperty(n, BindingFlags.Instance | BindingFlags.Public);
                if (p != null && p.CanRead) return MemberAccessor.FromProperty(p);

                var f = T.GetField(n, BindingFlags.Instance | BindingFlags.Public);
                if (f != null) return MemberAccessor.FromField(f);
            }
            return MemberAccessor.Missing;
        }
        public static bool IsEmpty(InventorySlotState s)
        {
            if (s == null) return true;
            var id = ReadId(s);
            var cnt = ReadCount(s);
            return string.IsNullOrEmpty(id) || cnt <= 0;
        }


        private readonly struct MemberAccessor
        {
            private readonly PropertyInfo _prop;
            private readonly FieldInfo _field;
            public bool IsMissing => _prop == null && _field == null;

            private MemberAccessor(PropertyInfo p, FieldInfo f) { _prop = p; _field = f; }
            public static MemberAccessor FromProperty(PropertyInfo p) => new MemberAccessor(p, null);
            public static MemberAccessor FromField(FieldInfo f) => new MemberAccessor(null, f);
            public static MemberAccessor Missing => new MemberAccessor(null, null);

            public object Get(object obj)
            {
                if (_prop != null) return _prop.GetValue(obj);
                if (_field != null) return _field.GetValue(obj);
                return null;
            }

            public void Set(object obj, object value)
            {
                if (_prop != null && _prop.CanWrite) { _prop.SetValue(obj, value); return; }
                if (_field != null) { _field.SetValue(obj, value); }
            }
        }
    }
}
