using System.Collections.Generic;

namespace GoodTimeStudio.MyPhone.OBEX.Headers
{
    public partial class AppParameterDictionary : Dictionary<byte, AppParameter>
    {
        public new AppParameter this[byte key]
        {
            get
            {
                if (TryGetValue(key, out var value))
                {
                    return value;
                }
                else
                {
                    throw new ObexAppParameterNotFoundException(key);
                }
            }
            set { base[key] = value; }
        }
    }
}
