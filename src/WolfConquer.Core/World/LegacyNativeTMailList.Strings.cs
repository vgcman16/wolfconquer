namespace WolfConquer.Core.World;

internal partial struct LegacyNativeTMailList
{
    // Literal native fixed signed-byte strings: no clearing, terminator, cap or UTF8 repair.
    public unsafe string title
    {
        get
        {
            fixed (sbyte* value = _title)
            {
                return new string(value);
            }
        }
        set
        {
            fixed (sbyte* ptr = _title)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    ptr[i] = unchecked((sbyte)value[i]);
                }
            }
        }
    }

    public unsafe string Sender_name
    {
        get
        {
            fixed (sbyte* sender_name = _Sender_name)
            {
                return new string(sender_name);
            }
        }
        set
        {
            fixed (sbyte* sender_name = _Sender_name)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    sender_name[i] = unchecked((sbyte)value[i]);
                }
            }
        }
    }

    public unsafe string content
    {
        get
        {
            fixed (sbyte* value = _content)
            {
                return new string(value);
            }
        }
        set
        {
            fixed (sbyte* ptr = _content)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    ptr[i] = unchecked((sbyte)value[i]);
                }
            }
        }
    }
}
